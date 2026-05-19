using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkforceManagement.Api.Data;
using WorkforceManagement.Api.Models;
using WorkforceManagement.Api.Models.Store;
using WorkforceManagement.Api.Services;

namespace WorkforceManagement.Api.Controllers;

[ApiController]
[Route("api/stores")]
[Authorize]
public class StoresController : ControllerBase
{
    private readonly AppDbContext _db;
    public StoresController(AppDbContext db) => _db = db;

    private string Role => User.FindFirstValue(ClaimTypes.Role) ?? "";
    private bool IsAdmin => Role == "Admin";
    private int? EmployeeId
    {
        get
        {
            var raw = User.FindFirstValue("employeeId");
            return int.TryParse(raw, out var id) && id > 0 ? id : null;
        }
    }

    /// <summary>Tất cả CH đang hoạt động — dùng cho dropdown đăng ký ca (NV chọn tự do).</summary>
    [HttpGet("options")]
    public async Task<IActionResult> GetOptions()
    {
        var stores = await _db.Stores
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new StoreDto
            {
                Id = s.Id,
                Name = s.Name,
                Address = s.Address,
                Phone = s.Phone,
                IsActive = s.IsActive,
                EmployeeCount = s.EmployeeStores.Count,
                StandardWorkHoursPerDay = s.StandardWorkHoursPerDay,
                OvertimeRateMultiplier = s.OvertimeRateMultiplier,
            })
            .ToListAsync();
        return Ok(ApiResponse<List<StoreDto>>.Ok(stores));
    }

    /// <summary>Cửa hàng được gán — Employee/Manager chỉ thấy CH của mình; Admin dùng GET /stores.</summary>
    [HttpGet("assigned")]
    public async Task<IActionResult> GetAssigned()
    {
        var scope = new UserStoreScope(_db, User);
        var storeIds = await scope.GetManagedStoreIdsAsync();

        IQueryable<Store> q = _db.Stores.Where(s => s.IsActive);
        if (storeIds != null)
        {
            if (storeIds.Count == 0) return Ok(ApiResponse<List<StoreDto>>.Ok(new()));
            q = q.Where(s => storeIds.Contains(s.Id));
        }

        var stores = await q
            .OrderBy(s => s.Name)
            .Select(s => new StoreDto
            {
                Id = s.Id,
                Name = s.Name,
                Address = s.Address,
                Phone = s.Phone,
                IsActive = s.IsActive,
                EmployeeCount = s.EmployeeStores.Count,
                StandardWorkHoursPerDay = s.StandardWorkHoursPerDay,
                OvertimeRateMultiplier = s.OvertimeRateMultiplier,
            })
            .ToListAsync();
        return Ok(ApiResponse<List<StoreDto>>.Ok(stores));
    }

    private static StoreDto MapStore(Store s, int employeeCount) => new()
    {
        Id = s.Id,
        Name = s.Name,
        Address = s.Address,
        Phone = s.Phone,
        IsActive = s.IsActive,
        EmployeeCount = employeeCount,
        StandardWorkHoursPerDay = s.StandardWorkHoursPerDay,
        OvertimeRateMultiplier = s.OvertimeRateMultiplier,
    };

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var scope = new UserStoreScope(_db, User);
        var storeIds = await scope.GetManagedStoreIdsAsync();
        if (storeIds != null && storeIds.Count == 0)
            return Ok(ApiResponse<List<StoreDto>>.Ok(new()));

        var q = _db.Stores.AsQueryable();
        if (storeIds != null)
            q = q.Where(s => storeIds.Contains(s.Id));

        var stores = await q
            .OrderBy(s => s.Name)
            .Select(s => new StoreDto
            {
                Id = s.Id,
                Name = s.Name,
                Address = s.Address,
                Phone = s.Phone,
                IsActive = s.IsActive,
                EmployeeCount = s.EmployeeStores.Count,
                StandardWorkHoursPerDay = s.StandardWorkHoursPerDay,
                OvertimeRateMultiplier = s.OvertimeRateMultiplier,
            })
            .ToListAsync();
        return Ok(ApiResponse<List<StoreDto>>.Ok(stores));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var scope = new UserStoreScope(_db, User);
        if (!await scope.CanAccessStoreAsync(id))
            return StatusCode(403, ApiResponse.Fail("Không có quyền xem cửa hàng này."));

        var s = await _db.Stores.FindAsync(id);
        if (s == null) return NotFound(ApiResponse.Fail("Không tìm thấy cửa hàng."));
        var count = await _db.EmployeeStores.CountAsync(es => es.StoreId == id);
        return Ok(ApiResponse<StoreDto>.Ok(MapStore(s, count)));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateStoreDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(ApiResponse.Fail("Tên cửa hàng không được để trống."));

        var store = new Store
        {
            Name = dto.Name.Trim(),
            Address = dto.Address,
            Phone = dto.Phone,
            StandardWorkHoursPerDay = dto.StandardWorkHoursPerDay is > 0 ? dto.StandardWorkHoursPerDay.Value : 8,
            OvertimeRateMultiplier = dto.OvertimeRateMultiplier is > 0 ? dto.OvertimeRateMultiplier.Value : 1.5m,
        };
        _db.Stores.Add(store);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<StoreDto>.Ok(MapStore(store, 0), "Tạo cửa hàng thành công."));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateStoreDto dto)
    {
        var store = await _db.Stores.FindAsync(id);
        if (store == null) return NotFound(ApiResponse.Fail("Không tìm thấy cửa hàng."));
        store.Name = dto.Name.Trim();
        store.Address = dto.Address;
        store.Phone = dto.Phone;
        if (dto.StandardWorkHoursPerDay is > 0) store.StandardWorkHoursPerDay = dto.StandardWorkHoursPerDay.Value;
        if (dto.OvertimeRateMultiplier is > 0) store.OvertimeRateMultiplier = dto.OvertimeRateMultiplier.Value;
        store.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse.Ok("Cập nhật thành công."));
    }

    [HttpPatch("{id:int}/toggle-active")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var store = await _db.Stores.FindAsync(id);
        if (store == null) return NotFound(ApiResponse.Fail("Không tìm thấy cửa hàng."));
        store.IsActive = !store.IsActive;
        store.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse.Ok(store.IsActive ? "Đã kích hoạt." : "Đã vô hiệu hóa."));
    }

    [HttpGet("{id:int}/employees")]
    public async Task<IActionResult> GetEmployees(int id)
    {
        var scope = new UserStoreScope(_db, User);
        if (!await scope.CanAccessStoreAsync(id))
            return StatusCode(403, ApiResponse.Fail("Không có quyền xem cửa hàng này."));

        var employees = await _db.EmployeeStores
            .Where(es => es.StoreId == id)
            .Include(es => es.Employee).ThenInclude(e => e.User)
            .Select(es => new { es.Employee.Id, es.Employee.EmployeeCode, es.Employee.FullName, es.Employee.User.Role, es.Employee.IsActive })
            .ToListAsync();
        return Ok(ApiResponse<object>.Ok(employees));
    }

    [HttpPost("{id:int}/employees")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AssignEmployee(int id, [FromBody] AssignEmployeeDto dto)
    {
        var exists = await _db.EmployeeStores.AnyAsync(es => es.StoreId == id && es.EmployeeId == dto.EmployeeId);
        if (exists) return BadRequest(ApiResponse.Fail("Nhân viên đã được gán vào cửa hàng này."));
        _db.EmployeeStores.Add(new EmployeeStore { StoreId = id, EmployeeId = dto.EmployeeId });
        await _db.SaveChangesAsync();
        return Ok(ApiResponse.Ok("Gán nhân viên thành công."));
    }

    [HttpDelete("{id:int}/employees/{employeeId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RemoveEmployee(int id, int employeeId)
    {
        var es = await _db.EmployeeStores.FirstOrDefaultAsync(x => x.StoreId == id && x.EmployeeId == employeeId);
        if (es == null) return NotFound(ApiResponse.Fail("Không tìm thấy."));
        _db.EmployeeStores.Remove(es);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse.Ok("Đã gỡ nhân viên."));
    }
}

public class AssignEmployeeDto { public int EmployeeId { get; set; } }
