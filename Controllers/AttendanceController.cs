using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkforceManagement.Api.Data;
using WorkforceManagement.Api.Models;
using WorkforceManagement.Api.Models.Attendance;
using WorkforceManagement.Api.Services;

namespace WorkforceManagement.Api.Controllers;

[ApiController]
[Route("api/attendance")]
[Authorize]
public class AttendanceController : ControllerBase
{
    private readonly AppDbContext _db;
    public AttendanceController(AppDbContext db) => _db = db;

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
        [FromQuery] string? dateFrom, [FromQuery] string? dateTo)
    {
        var q = _db.Attendances
            .Include(a => a.Employee)
            .Include(a => a.Store)
            .AsQueryable();

        if (Role == "Employee")
        {
            if (EmployeeId == null) return Ok(ApiResponse<List<AttendanceDto>>.Ok(new()));
            q = q.Where(a => a.EmployeeId == EmployeeId.Value);
        }
        else
        {
            var scope = new UserStoreScope(_db, User);
            var managedStoreIds = await scope.GetManagedStoreIdsAsync();
            if (managedStoreIds != null)
            {
                if (managedStoreIds.Count == 0) return Ok(ApiResponse<List<AttendanceDto>>.Ok(new()));
                if (storeId.HasValue && !managedStoreIds.Contains(storeId.Value))
                    return StatusCode(403, ApiResponse.Fail("Không có quyền xem cửa hàng này."));
                q = q.Where(a => managedStoreIds.Contains(a.StoreId));
            }

            if (employeeId.HasValue) q = q.Where(a => a.EmployeeId == employeeId.Value);
            if (storeId.HasValue) q = q.Where(a => a.StoreId == storeId.Value);
        }

        if (DateOnly.TryParse(dateFrom, out var df)) q = q.Where(a => a.WorkDate >= df);
        if (DateOnly.TryParse(dateTo, out var dt)) q = q.Where(a => a.WorkDate <= dt);

        var list = await q.OrderByDescending(a => a.WorkDate).ThenByDescending(a => a.CheckIn).ToListAsync();
        var dtos = new List<AttendanceDto>();
        foreach (var a in list)
            dtos.Add(await MapDtoAsync(a));
        return Ok(ApiResponse<List<AttendanceDto>>.Ok(dtos));
    }

    /// <summary>Bản ghi chấm công hôm nay đang mở (đã vào, chưa ra).</summary>
    [HttpGet("today-open")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> GetTodayOpen([FromQuery] int? storeId, [FromQuery] int? employeeId)
    {
        if (employeeId == null)
            return Ok(ApiResponse<AttendanceDto?>.Ok(null));

        var today = DateOnly.FromDateTime(DateTime.Now);
        var q = _db.Attendances
            .Include(a => a.Employee)
            .Include(a => a.Store)
            .Where(a => a.EmployeeId == employeeId.Value
                && a.WorkDate == today
                && a.CheckOut == null
                && a.Status == AttendanceStatuses.Worked);

        if (storeId.HasValue) q = q.Where(a => a.StoreId == storeId.Value);

        var open = await q.OrderByDescending(a => a.Id).FirstOrDefaultAsync();
        if (open == null) return Ok(ApiResponse<AttendanceDto?>.Ok(null));
        return Ok(ApiResponse<AttendanceDto?>.Ok(await MapDtoAsync(open)));
    }

    /// <summary>Giờ chuẩn / hệ số OT từ DB (ca đăng ký hoặc cấu hình cửa hàng).</summary>
    [HttpGet("work-context")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> GetWorkContext(
        [FromQuery] int employeeId, [FromQuery] int storeId, [FromQuery] string workDate)
    {
        if (!DateOnly.TryParse(workDate, out var wd))
            return BadRequest(ApiResponse.Fail("workDate không hợp lệ."));
        try
        {
            var (standardHours, otMultiplier, shiftName) =
                await AttendanceRules.GetWorkContextAsync(_db, employeeId, storeId, wd);
            return Ok(ApiResponse<object>.Ok(new
            {
                standardHours,
                overtimeRateMultiplier = otMultiplier,
                shiftName,
                source = shiftName != null ? "shift_registration" : "store_settings"
            }));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpGet("summary")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> GetSummary([FromQuery] int? storeId, [FromQuery] int month, [FromQuery] int year)
    {
        var scope = new UserStoreScope(_db, User);
        var managedStoreIds = await scope.GetManagedStoreIdsAsync();
        if (managedStoreIds != null && managedStoreIds.Count == 0)
            return Ok(ApiResponse<List<AttendanceSummaryDto>>.Ok(new()));
        if (storeId.HasValue && !await scope.IsStoreFilterAllowedAsync(storeId))
            return StatusCode(403, ApiResponse.Fail("Không có quyền xem cửa hàng này."));

        var dateFrom = new DateOnly(year, month, 1);
        var dateTo = dateFrom.AddMonths(1).AddDays(-1);

        var q = _db.Attendances
            .Include(a => a.Employee)
            .Where(a => a.WorkDate >= dateFrom && a.WorkDate <= dateTo
                && a.Status == AttendanceStatuses.Worked
                && a.CheckOut != null);

        if (managedStoreIds != null)
            q = q.Where(a => managedStoreIds.Contains(a.StoreId));
        if (storeId.HasValue) q = q.Where(a => a.StoreId == storeId.Value);

        var list = await q.ToListAsync();
        var summary = list
            .GroupBy(a => a.EmployeeId)
            .Select(g => new AttendanceSummaryDto
            {
                EmployeeId = g.Key,
                EmployeeName = g.First().Employee?.FullName ?? "",
                EmployeeCode = g.First().Employee?.EmployeeCode ?? "",
                WorkedDays = g.Count(),
                WorkedHours = g.Sum(a => a.WorkedHours),
                OvertimeHours = g.Sum(a => a.OvertimeHours)
            })
            .OrderBy(s => s.EmployeeName)
            .ToList();

        return Ok(ApiResponse<List<AttendanceSummaryDto>>.Ok(summary));
    }

    /// <summary>Đã tắt tự chấm — quản lý dùng POST /attendance (Nhập giờ).</summary>
    [HttpPost("check-in")]
    [Authorize]
    public IActionResult CheckIn([FromBody] CheckInDto dto)
    {
        return StatusCode(403, ApiResponse.Fail("Nhân viên không được tự chấm công. Quản lý nhập giờ vào/ra qua «Nhập giờ»."));
    }

    [HttpPost("{id:int}/check-out")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> CheckOut(int id)
    {
        var a = await _db.Attendances
            .Include(x => x.Employee)
            .Include(x => x.Store)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (a == null) return NotFound(ApiResponse.Fail("Không tìm thấy bản ghi."));
        if (a.CheckOut != null) return BadRequest(ApiResponse.Fail("Ca này đã chấm ra."));
        if (a.Status != AttendanceStatuses.Worked)
            return BadRequest(ApiResponse.Fail("Chỉ chấm ra cho bản ghi đi làm."));

        var scope = new UserStoreScope(_db, User);
        if (!await scope.CanAccessStoreAsync(a.StoreId))
            return StatusCode(403, ApiResponse.Fail("Không có quyền chấm công tại cửa hàng này."));

        var now = TimeOnly.FromDateTime(DateTime.Now);
        if (now <= a.CheckIn)
            return BadRequest(ApiResponse.Fail("Giờ ra phải sau giờ vào."));

        var standard = await AttendanceRules.GetStandardHoursAsync(_db, a.EmployeeId, a.StoreId, a.WorkDate);
        AttendanceRules.ApplyWorkedTimes(a, a.CheckIn, now, standard);
        a.UpdatedBy = CurrentUserId;
        a.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(ApiResponse<AttendanceDto>.Ok(await MapDtoAsync(a), "Đã chấm ra ca."));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Create([FromBody] CreateAttendanceDto dto)
    {
        var scope = new UserStoreScope(_db, User);
        if (!await scope.CanAccessStoreAsync(dto.StoreId) || !await scope.CanAccessEmployeeAsync(dto.EmployeeId))
            return StatusCode(403, ApiResponse.Fail("Không có quyền thao tác cửa hàng/nhân viên này."));

        if (!DateOnly.TryParse(dto.WorkDate, out var workDate))
            return BadRequest(ApiResponse.Fail("WorkDate không hợp lệ."));
        if (workDate > DateOnly.FromDateTime(DateTime.Today))
            return BadRequest(ApiResponse.Fail("Không thể chấm công cho ngày tương lai."));
        if (!TimeOnly.TryParse(dto.CheckIn, out var checkIn) || !TimeOnly.TryParse(dto.CheckOut, out var checkOut))
            return BadRequest(ApiResponse.Fail("Giờ vào/ra không hợp lệ (HH:mm)."));
        if (checkOut <= checkIn)
            return BadRequest(ApiResponse.Fail("Giờ ra phải sau giờ vào."));

        var duplicate = await _db.Attendances.AnyAsync(a =>
            a.EmployeeId == dto.EmployeeId && a.StoreId == dto.StoreId && a.WorkDate == workDate);
        if (duplicate) return BadRequest(ApiResponse.Fail("Đã có bản ghi chấm công cho nhân viên này ngày này."));

        var standard = await AttendanceRules.GetStandardHoursAsync(_db, dto.EmployeeId, dto.StoreId, workDate);
        var attendance = new Attendance
        {
            EmployeeId = dto.EmployeeId,
            StoreId = dto.StoreId,
            WorkDate = workDate,
            Note = dto.Note,
            CreatedBy = CurrentUserId
        };
        AttendanceRules.ApplyWorkedTimes(attendance, checkIn, checkOut, standard);
        _db.Attendances.Add(attendance);
        await _db.SaveChangesAsync();

        await _db.Entry(attendance).Reference(a => a.Employee).LoadAsync();
        await _db.Entry(attendance).Reference(a => a.Store).LoadAsync();
        return Ok(ApiResponse<AttendanceDto>.Ok(await MapDtoAsync(attendance), "Chấm công thành công."));
    }

    [HttpPost("mark-absent")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> MarkAbsent([FromBody] MarkAbsentDto dto)
    {
        var scope = new UserStoreScope(_db, User);
        if (!await scope.CanAccessStoreAsync(dto.StoreId) || !await scope.CanAccessEmployeeAsync(dto.EmployeeId))
            return StatusCode(403, ApiResponse.Fail("Không có quyền thao tác cửa hàng/nhân viên này."));

        if (!DateOnly.TryParse(dto.WorkDate, out var workDate))
            return BadRequest(ApiResponse.Fail("WorkDate không hợp lệ."));

        var duplicate = await _db.Attendances.AnyAsync(a =>
            a.EmployeeId == dto.EmployeeId && a.StoreId == dto.StoreId && a.WorkDate == workDate);
        if (duplicate) return BadRequest(ApiResponse.Fail("Đã có bản ghi cho ngày này."));

        var attendance = new Attendance
        {
            EmployeeId = dto.EmployeeId,
            StoreId = dto.StoreId,
            WorkDate = workDate,
            CheckIn = new TimeOnly(0, 0),
            CheckOut = new TimeOnly(0, 1),
            Status = AttendanceStatuses.Absent,
            OvertimeHours = 0,
            Note = dto.Note,
            CreatedBy = CurrentUserId
        };
        _db.Attendances.Add(attendance);
        await _db.SaveChangesAsync();
        await _db.Entry(attendance).Reference(a => a.Employee).LoadAsync();
        await _db.Entry(attendance).Reference(a => a.Store).LoadAsync();
        return Ok(ApiResponse<AttendanceDto>.Ok(await MapDtoAsync(attendance), "Đã ghi nhận không đi làm."));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateAttendanceDto dto)
    {
        var a = await _db.Attendances.FindAsync(id);
        if (a == null) return NotFound(ApiResponse.Fail("Không tìm thấy bản ghi."));
        var scope = new UserStoreScope(_db, User);
        if (!await scope.CanAccessStoreAsync(a.StoreId))
            return StatusCode(403, ApiResponse.Fail("Không có quyền sửa bản ghi này."));
        if (a.Status == AttendanceStatuses.Absent)
            return BadRequest(ApiResponse.Fail("Bản ghi vắng không chỉnh giờ. Xóa và tạo mới nếu cần."));
        if (!TimeOnly.TryParse(dto.CheckIn, out var checkIn) || !TimeOnly.TryParse(dto.CheckOut, out var checkOut))
            return BadRequest(ApiResponse.Fail("Giờ không hợp lệ."));
        if (checkOut <= checkIn) return BadRequest(ApiResponse.Fail("Giờ ra phải sau giờ vào."));

        var standard = await AttendanceRules.GetStandardHoursAsync(_db, a.EmployeeId, a.StoreId, a.WorkDate);
        AttendanceRules.ApplyWorkedTimes(a, checkIn, checkOut, standard);
        a.Note = dto.Note;
        a.UpdatedBy = CurrentUserId;
        a.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse.Ok("Cập nhật thành công."));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Delete(int id)
    {
        var a = await _db.Attendances.FindAsync(id);
        if (a == null) return NotFound(ApiResponse.Fail("Không tìm thấy bản ghi."));
        var scope = new UserStoreScope(_db, User);
        if (!await scope.CanAccessStoreAsync(a.StoreId))
            return StatusCode(403, ApiResponse.Fail("Không có quyền xóa bản ghi này."));
        _db.Attendances.Remove(a);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse.Ok("Đã xóa bản ghi chấm công."));
    }

    private async Task<AttendanceDto> MapDtoAsync(Attendance a)
    {
        var standard = a.Status == AttendanceStatuses.Worked
            ? await AttendanceRules.GetStandardHoursAsync(_db, a.EmployeeId, a.StoreId, a.WorkDate)
            : 0;
        var status = a.Status;
        return new AttendanceDto
        {
            Id = a.Id,
            EmployeeId = a.EmployeeId,
            EmployeeName = a.Employee?.FullName ?? "",
            EmployeeCode = a.Employee?.EmployeeCode ?? "",
            StoreId = a.StoreId,
            StoreName = a.Store?.Name ?? "",
            WorkDate = a.WorkDate.ToString("yyyy-MM-dd"),
            CheckIn = a.CheckIn.ToString("HH:mm"),
            CheckOut = a.CheckOut?.ToString("HH:mm"),
            IsOpen = a.IsOpen,
            WorkedHours = a.WorkedHours,
            OvertimeHours = a.OvertimeHours,
            StandardHours = standard,
            Status = status,
            StatusLabel = AttendanceStatuses.Label(status),
            Note = a.Note,
            CreatedAt = a.CreatedAt.ToString("yyyy-MM-dd HH:mm")
        };
    }
}
