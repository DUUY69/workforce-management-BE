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
[Route("api/shifts")]
[Authorize]
public class ShiftsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ShiftsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? storeId, [FromQuery] bool? isActive)
    {
        var scope = new UserStoreScope(_db, User);
        var managedStoreIds = await scope.GetManagedStoreIdsAsync();
        if (managedStoreIds != null && managedStoreIds.Count == 0)
            return Ok(ApiResponse<List<ShiftDto>>.Ok(new()));
        if (storeId.HasValue && !await scope.IsStoreFilterAllowedAsync(storeId))
            return StatusCode(403, ApiResponse.Fail("Không có quyền xem cửa hàng này."));

        var q = _db.Shifts.Include(s => s.Store).AsQueryable();
        if (managedStoreIds != null)
            q = q.Where(s => managedStoreIds.Contains(s.StoreId));
        if (storeId.HasValue) q = q.Where(s => s.StoreId == storeId.Value);
        if (isActive.HasValue) q = q.Where(s => s.IsActive == isActive.Value);
        var list = await q.OrderBy(s => s.StoreId).ThenBy(s => s.StartTime).ToListAsync();
        return Ok(ApiResponse<List<ShiftDto>>.Ok(list.Select(MapDto).ToList()));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Create([FromBody] CreateShiftDto dto)
    {
        if (!TimeOnly.TryParse(dto.StartTime, out var start) || !TimeOnly.TryParse(dto.EndTime, out var end))
            return BadRequest(ApiResponse.Fail("Giờ không hợp lệ (HH:mm)."));
        if (end <= start)
            return BadRequest(ApiResponse.Fail("Giờ kết thúc phải sau giờ bắt đầu."));

        var shift = new Shift { StoreId = dto.StoreId, Name = dto.Name.Trim(), StartTime = start, EndTime = end };
        _db.Shifts.Add(shift);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<ShiftDto>.Ok(MapDto(shift), "Tạo ca thành công."));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateShiftDto dto)
    {
        var shift = await _db.Shifts.FindAsync(id);
        if (shift == null) return NotFound(ApiResponse.Fail("Không tìm thấy ca."));
        if (!TimeOnly.TryParse(dto.StartTime, out var start) || !TimeOnly.TryParse(dto.EndTime, out var end))
            return BadRequest(ApiResponse.Fail("Giờ không hợp lệ."));
        if (end <= start) return BadRequest(ApiResponse.Fail("Giờ kết thúc phải sau giờ bắt đầu."));
        shift.Name = dto.Name.Trim();
        shift.StartTime = start;
        shift.EndTime = end;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse.Ok("Cập nhật thành công."));
    }

    [HttpPatch("{id:int}/toggle-active")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var shift = await _db.Shifts.FindAsync(id);
        if (shift == null) return NotFound(ApiResponse.Fail("Không tìm thấy ca."));
        shift.IsActive = !shift.IsActive;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse.Ok(shift.IsActive ? "Đã kích hoạt." : "Đã vô hiệu hóa."));
    }

    private static ShiftDto MapDto(Shift s) => new()
    {
        Id = s.Id, StoreId = s.StoreId, StoreName = s.Store?.Name ?? "",
        Name = s.Name, StartTime = s.StartTime.ToString("HH:mm"), EndTime = s.EndTime.ToString("HH:mm"),
        WorkHours = (s.EndTime - s.StartTime).TotalHours, IsActive = s.IsActive
    };
}
