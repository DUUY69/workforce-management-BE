using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Mvc;

using Microsoft.EntityFrameworkCore;

using WorkforceManagement.Api.Data;

using WorkforceManagement.Api.Models;

using WorkforceManagement.Api.Models.Shift;

using WorkforceManagement.Api.Services;



namespace WorkforceManagement.Api.Controllers;



[ApiController]

[Route("api/shift-registrations")]

[Authorize]

public class ShiftRegistrationsController : ControllerBase

{

    private readonly AppDbContext _db;

    public ShiftRegistrationsController(AppDbContext db) => _db = db;



    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

    private string Role => User.FindFirstValue(ClaimTypes.Role) ?? "";

    private int? EmployeeId

    {

        get

        {

            var raw = User.FindFirstValue("employeeId");

            return int.TryParse(raw, out var id) && id > 0 ? id : null;

        }

    }



    [HttpGet]

    public async Task<IActionResult> GetAll(

        [FromQuery] int? storeId, [FromQuery] int? employeeId,

        [FromQuery] string? status, [FromQuery] string? dateFrom, [FromQuery] string? dateTo)

    {

        var q = _db.ShiftRegistrations

            .Include(r => r.Employee)

            .Include(r => r.Shift)

            .Include(r => r.Store)

            .AsQueryable();



        if (Role == "Employee")

        {

            if (EmployeeId == null) return Ok(ApiResponse<List<ShiftRegistrationDto>>.Ok(new()));

            q = q.Where(r => r.EmployeeId == EmployeeId.Value);

        }

        else

        {

            var scope = new UserStoreScope(_db, User);

            var managedStoreIds = await scope.GetManagedStoreIdsAsync();

            if (managedStoreIds != null)

            {

                if (managedStoreIds.Count == 0) return Ok(ApiResponse<List<ShiftRegistrationDto>>.Ok(new()));

                if (storeId.HasValue && !managedStoreIds.Contains(storeId.Value))

                    return StatusCode(403, ApiResponse.Fail("Không có quyền xem cửa hàng này."));

                q = q.Where(r => managedStoreIds.Contains(r.StoreId));

            }



            if (employeeId.HasValue) q = q.Where(r => r.EmployeeId == employeeId.Value);

            if (storeId.HasValue) q = q.Where(r => r.StoreId == storeId.Value);

        }



        if (!string.IsNullOrEmpty(status)) q = q.Where(r => r.Status == status);

        if (DateOnly.TryParse(dateFrom, out var df)) q = q.Where(r => r.WorkDate >= df);

        if (DateOnly.TryParse(dateTo, out var dt)) q = q.Where(r => r.WorkDate <= dt);



        var list = await q.OrderByDescending(r => r.WorkDate).ThenBy(r => r.StartTime).ToListAsync();

        return Ok(ApiResponse<List<ShiftRegistrationDto>>.Ok(list.Select(MapDto).ToList()));

    }



    [HttpPost]

    [Authorize(Roles = "Employee")]

    public async Task<IActionResult> Create([FromBody] CreateShiftRegistrationDto dto)

    {

        if (EmployeeId == null) return BadRequest(ApiResponse.Fail("Tài khoản không phải nhân viên."));

        if (!DateOnly.TryParse(dto.WorkDate, out var workDate))

            return BadRequest(ApiResponse.Fail("Ngày làm không hợp lệ (yyyy-MM-dd)."));

        if (workDate <= DateOnly.FromDateTime(DateTime.Today))

            return BadRequest(ApiResponse.Fail("Không thể đăng ký ca cho ngày hôm nay hoặc quá khứ."));

        if (!TimeOnly.TryParse(dto.StartTime, out var startTime) || !TimeOnly.TryParse(dto.EndTime, out var endTime))

            return BadRequest(ApiResponse.Fail("Giờ làm không hợp lệ (HH:mm)."));

        if (endTime <= startTime)

            return BadRequest(ApiResponse.Fail("Giờ kết thúc phải sau giờ bắt đầu."));



        var storeOk = await _db.Stores.AnyAsync(s => s.Id == dto.StoreId && s.IsActive);
        if (!storeOk)
            return BadRequest(ApiResponse.Fail("Cửa hàng không tồn tại hoặc đã ngừng hoạt động."));

        var overlap = await FindTimeOverlapAsync(EmployeeId.Value, workDate, startTime, endTime, excludeId: null);
        if (overlap != null)
        {
            var storeName = overlap.Store?.Name ?? "cửa hàng khác";
            return BadRequest(ApiResponse.Fail(
                $"Trùng khung giờ với ca đã đăng ký tại {storeName} ({overlap.StartTime:HH:mm}–{overlap.EndTime:HH:mm}). " +
                "Một ngày không thể làm hai nơi cùng lúc — hãy đổi giờ hoặc đổi ngày."));
        }

        _db.ShiftRegistrations.Add(new ShiftRegistration

        {

            EmployeeId = EmployeeId.Value,

            ShiftId = null,

            StartTime = startTime,

            EndTime = endTime,

            StoreId = dto.StoreId,

            WorkDate = workDate,

            Status = "Pending"

        });

        await _db.SaveChangesAsync();

        return Ok(ApiResponse.Ok("Đăng ký ca thành công."));

    }



    [HttpPatch("{id:int}/approve")]

    [Authorize(Roles = "Admin,Manager")]

    public async Task<IActionResult> Approve(int id)

    {

        var reg = await _db.ShiftRegistrations.FindAsync(id);

        if (reg == null) return NotFound(ApiResponse.Fail("Không tìm thấy đăng ký."));

        var scope = new UserStoreScope(_db, User);

        if (!await scope.CanAccessStoreAsync(reg.StoreId))

            return StatusCode(403, ApiResponse.Fail("Không có quyền duyệt đăng ký cửa hàng này."));

        if (reg.Status != "Pending") return BadRequest(ApiResponse.Fail("Chỉ có thể duyệt đăng ký đang Pending."));

        var overlapOnApprove = await FindTimeOverlapAsync(reg.EmployeeId, reg.WorkDate, reg.StartTime, reg.EndTime, excludeId: reg.Id);
        if (overlapOnApprove != null)
        {
            var storeName = overlapOnApprove.Store?.Name ?? "cửa hàng khác";
            return BadRequest(ApiResponse.Fail(
                $"Không thể duyệt: trùng giờ với ca tại {storeName} ({overlapOnApprove.StartTime:HH:mm}–{overlapOnApprove.EndTime:HH:mm})."));
        }

        reg.Status = "Approved";

        reg.ReviewedBy = CurrentUserId;

        reg.ReviewedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(ApiResponse.Ok("Đã duyệt."));

    }



    [HttpPatch("{id:int}/reject")]

    [Authorize(Roles = "Admin,Manager")]

    public async Task<IActionResult> Reject(int id, [FromBody] ReviewShiftRegistrationDto dto)

    {

        var reg = await _db.ShiftRegistrations.FindAsync(id);

        if (reg == null) return NotFound(ApiResponse.Fail("Không tìm thấy đăng ký."));

        var scope = new UserStoreScope(_db, User);

        if (!await scope.CanAccessStoreAsync(reg.StoreId))

            return StatusCode(403, ApiResponse.Fail("Không có quyền từ chối đăng ký cửa hàng này."));

        if (reg.Status != "Pending") return BadRequest(ApiResponse.Fail("Chỉ có thể từ chối đăng ký đang Pending."));

        reg.Status = "Rejected";

        reg.RejectReason = dto.RejectReason;

        reg.ReviewedBy = CurrentUserId;

        reg.ReviewedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(ApiResponse.Ok("Đã từ chối."));

    }



    [HttpPatch("{id:int}/cancel")]

    [Authorize(Roles = "Employee")]

    public async Task<IActionResult> Cancel(int id)

    {

        var reg = await _db.ShiftRegistrations.FindAsync(id);

        if (reg == null) return NotFound(ApiResponse.Fail("Không tìm thấy đăng ký."));

        if (reg.EmployeeId != EmployeeId) return StatusCode(403, ApiResponse.Fail("Không có quyền."));

        if (reg.Status != "Pending") return BadRequest(ApiResponse.Fail("Chỉ có thể hủy đăng ký đang Pending."));

        reg.Status = "Cancelled";

        await _db.SaveChangesAsync();

        return Ok(ApiResponse.Ok("Đã hủy đăng ký."));

    }



    private async Task<ShiftRegistration?> FindTimeOverlapAsync(
        int employeeId, DateOnly workDate, TimeOnly start, TimeOnly end, int? excludeId)
    {
        var q = _db.ShiftRegistrations
            .Include(r => r.Store)
            .Where(r => r.EmployeeId == employeeId
                && r.WorkDate == workDate
                && (r.Status == "Pending" || r.Status == "Approved")
                && r.StartTime < end && start < r.EndTime);
        if (excludeId.HasValue)
            q = q.Where(r => r.Id != excludeId.Value);
        return await q.FirstOrDefaultAsync();
    }

    private static ShiftRegistrationDto MapDto(ShiftRegistration r)

    {

        var start = r.StartTime;

        var end = r.EndTime;

        var timeLabel = $"{start:HH\\:mm} – {end:HH\\:mm}";

        return new ShiftRegistrationDto

        {

            Id = r.Id,

            EmployeeId = r.EmployeeId,

            EmployeeName = r.Employee?.FullName ?? "",

            ShiftId = r.ShiftId,

            ShiftName = r.Shift?.Name ?? "",

            StartTime = start.ToString("HH:mm"),

            EndTime = end.ToString("HH:mm"),

            ShiftTime = timeLabel,

            StoreId = r.StoreId,

            StoreName = r.Store?.Name ?? "",

            WorkDate = r.WorkDate.ToString("yyyy-MM-dd"),

            Status = r.Status,

            RejectReason = r.RejectReason,

            CreatedAt = r.CreatedAt.ToString("yyyy-MM-dd HH:mm")

        };

    }

}


