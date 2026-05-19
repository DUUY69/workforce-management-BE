using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkforceManagement.Api.Data;
using WorkforceManagement.Api.Models;
using WorkforceManagement.Api.Models.Employee;
using WorkforceManagement.Api.Services;

namespace WorkforceManagement.Api.Controllers;

[ApiController]
[Route("api/employees")]
[Authorize]
public class EmployeesController : ControllerBase
{
    private readonly AppDbContext _db;
    public EmployeesController(AppDbContext db) => _db = db;

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
    private string Role => User.FindFirstValue(ClaimTypes.Role) ?? "";
    private bool IsAdmin => Role == "Admin";
    private bool IsManager => Role == "Manager";
    private bool CanManageSalary => IsAdmin || IsManager;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? storeId, [FromQuery] bool? isActive)
    {
        var scope = new UserStoreScope(_db, User);
        var managedStoreIds = await scope.GetManagedStoreIdsAsync();
        if (managedStoreIds != null && managedStoreIds.Count == 0)
            return Ok(ApiResponse<List<EmployeeDto>>.Ok(new()));

        if (storeId.HasValue && !await scope.IsStoreFilterAllowedAsync(storeId))
            return StatusCode(403, ApiResponse.Fail("Không có quyền xem cửa hàng này."));

        var q = _db.Employees
            .Include(e => e.User)
            .Include(e => e.EmployeeStores).ThenInclude(es => es.Store)
            .Include(e => e.SalaryCoefficients)
            .AsQueryable();

        if (managedStoreIds != null)
            q = q.Where(e => e.EmployeeStores.Any(es => managedStoreIds.Contains(es.StoreId)));
        if (storeId.HasValue)
            q = q.Where(e => e.EmployeeStores.Any(es => es.StoreId == storeId.Value));
        if (isActive.HasValue)
            q = q.Where(e => e.IsActive == isActive.Value);

        var list = await q.OrderBy(e => e.FullName).ToListAsync();
        return Ok(ApiResponse<List<EmployeeDto>>.Ok(list.Select(MapDto).ToList()));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var scope = new UserStoreScope(_db, User);
        if (!await scope.CanAccessEmployeeAsync(id))
            return StatusCode(403, ApiResponse.Fail("Không có quyền xem nhân viên này."));

        var e = await _db.Employees
            .Include(x => x.User)
            .Include(x => x.EmployeeStores).ThenInclude(es => es.Store)
            .Include(x => x.SalaryCoefficients)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (e == null) return NotFound(ApiResponse.Fail("Không tìm thấy nhân viên."));
        return Ok(ApiResponse<EmployeeDto>.Ok(MapDto(e)));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateEmployeeDto dto)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(dto.FullName)) errors.Add("Họ tên không được để trống.");
        if (string.IsNullOrWhiteSpace(dto.Username)) errors.Add("Username không được để trống.");
        if (string.IsNullOrWhiteSpace(dto.Email)) errors.Add("Email không được để trống.");
        if (string.IsNullOrWhiteSpace(dto.Password)) errors.Add("Mật khẩu không được để trống.");
        if (errors.Any()) return BadRequest(ApiResponse.Fail("Dữ liệu không hợp lệ.", errors));

        if (await _db.Users.AnyAsync(u => u.Username == dto.Username))
            return BadRequest(ApiResponse.Fail("Username đã tồn tại."));
        if (await _db.Users.AnyAsync(u => u.Email == dto.Email))
            return BadRequest(ApiResponse.Fail("Email đã tồn tại."));

        // Tạo user
        var user = new User
        {
            Username = dto.Username.Trim(),
            Email = dto.Email.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = dto.Role
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // Sinh mã nhân viên
        var count = await _db.Employees.CountAsync();
        var code = $"NV{(count + 1):D3}";

        var employee = new Employee
        {
            UserId = user.Id,
            EmployeeCode = code,
            FullName = dto.FullName.Trim(),
            Phone = dto.Phone,
            Address = dto.Address,
            NationalId = dto.NationalId,
            Gender = dto.Gender,
            BankAccountNo = dto.BankAccountNo,
            BankName = dto.BankName,
            BankAccountName = dto.BankAccountName,
            StartDate = DateOnly.TryParse(dto.StartDate, out var sd) ? sd : DateOnly.FromDateTime(DateTime.Today)
        };
        if (dto.DateOfBirth != null && DateOnly.TryParse(dto.DateOfBirth, out var dob))
            employee.DateOfBirth = dob;

        _db.Employees.Add(employee);
        await _db.SaveChangesAsync();

        // Gán cửa hàng
        foreach (var sid in dto.StoreIds.Distinct())
            _db.EmployeeStores.Add(new EmployeeStore { EmployeeId = employee.Id, StoreId = sid });

        // Hệ số lương ban đầu
        if (dto.BaseSalaryPerHour.HasValue)
        {
            _db.SalaryCoefficients.Add(new SalaryCoefficient
            {
                EmployeeId = employee.Id,
                BaseSalaryPerHour = dto.BaseSalaryPerHour.Value,
                Coefficient = dto.Coefficient ?? 1.0m,
                EffectiveFrom = DateOnly.FromDateTime(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)),
                CreatedBy = CurrentUserId
            });
        }

        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { id = employee.Id, code = employee.EmployeeCode }, "Tạo nhân viên thành công."));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateEmployeeDto dto)
    {
        var emp = await _db.Employees.Include(e => e.EmployeeStores).FirstOrDefaultAsync(e => e.Id == id);
        if (emp == null) return NotFound(ApiResponse.Fail("Không tìm thấy nhân viên."));

        emp.FullName = dto.FullName.Trim();
        emp.Phone = dto.Phone;
        emp.Address = dto.Address;
        emp.EmergencyContact = dto.EmergencyContact;
        emp.NationalId = dto.NationalId;
        emp.Gender = dto.Gender;
        emp.BankAccountNo = dto.BankAccountNo;
        emp.BankName = dto.BankName;
        emp.BankAccountName = dto.BankAccountName;
        emp.UpdatedAt = DateTime.UtcNow;
        if (dto.DateOfBirth != null && DateOnly.TryParse(dto.DateOfBirth, out var dob)) emp.DateOfBirth = dob;

        // Cập nhật store assignments
        _db.EmployeeStores.RemoveRange(emp.EmployeeStores);
        foreach (var sid in dto.StoreIds.Distinct())
            _db.EmployeeStores.Add(new EmployeeStore { EmployeeId = id, StoreId = sid });

        await _db.SaveChangesAsync();
        return Ok(ApiResponse.Ok("Cập nhật thành công."));
    }

    [HttpPatch("{id:int}/toggle-active")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var emp = await _db.Employees.Include(e => e.User).FirstOrDefaultAsync(e => e.Id == id);
        if (emp == null) return NotFound(ApiResponse.Fail("Không tìm thấy nhân viên."));
        emp.IsActive = !emp.IsActive;
        emp.User.IsActive = emp.IsActive;
        emp.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse.Ok(emp.IsActive ? "Đã kích hoạt." : "Đã vô hiệu hóa."));
    }

    [HttpGet("me/salary-history")]
    public async Task<IActionResult> GetMySalaryHistory()
    {
        var empId = await GetCurrentEmployeeIdAsync();
        if (empId == null) return NotFound(ApiResponse.Fail("Tài khoản chưa liên kết nhân viên."));
        return await GetSalaryHistoryInternal(empId.Value, includeActor: false);
    }

    [HttpGet("{id:int}/salary-history")]
    public async Task<IActionResult> GetSalaryHistory(int id)
    {
        if (!CanManageSalary)
        {
            var myId = await GetCurrentEmployeeIdAsync();
            if (myId != id) return Forbid();
            return await GetSalaryHistoryInternal(id, includeActor: false);
        }

        var scope = new UserStoreScope(_db, User);
        if (!await scope.CanAccessEmployeeAsync(id))
            return StatusCode(403, ApiResponse.Fail("Không có quyền xem nhân viên này."));

        return await GetSalaryHistoryInternal(id, includeActor: true);
    }

    [HttpPost("{id:int}/salary-coefficients")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> AddSalaryCoefficient(int id, [FromBody] CreateSalaryCoefficientDto dto)
    {
        var scope = new UserStoreScope(_db, User);
        if (!await scope.CanAccessEmployeeAsync(id))
            return StatusCode(403, ApiResponse.Fail("Không có quyền chỉnh lương nhân viên này."));
        // Mặc định: ngày 1 tháng sau (không cần client gửi ngày)
        var nextMonth = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(1);
        DateOnly effectiveFrom;
        if (string.IsNullOrWhiteSpace(dto.EffectiveFrom))
            effectiveFrom = nextMonth;
        else if (!DateOnly.TryParse(dto.EffectiveFrom, out effectiveFrom))
            return BadRequest(ApiResponse.Fail("EffectiveFrom không hợp lệ (yyyy-MM-dd)."));
        else if (effectiveFrom < nextMonth)
            return BadRequest(ApiResponse.Fail($"EffectiveFrom phải từ {nextMonth:yyyy-MM-dd} trở đi (tháng sau)."));
        else if (effectiveFrom.Day != 1)
            return BadRequest(ApiResponse.Fail("EffectiveFrom phải là ngày 1 của tháng."));

        _db.SalaryCoefficients.Add(new SalaryCoefficient
        {
            EmployeeId = id,
            BaseSalaryPerHour = dto.BaseSalaryPerHour,
            Coefficient = dto.Coefficient,
            EffectiveFrom = effectiveFrom,
            Note = dto.Note,
            CreatedBy = CurrentUserId
        });
        await _db.SaveChangesAsync();
        return Ok(ApiResponse.Ok("Thêm hệ số lương thành công."));
    }

    private async Task<int?> GetCurrentEmployeeIdAsync()
    {
        return await _db.Employees
            .Where(e => e.UserId == CurrentUserId)
            .Select(e => (int?)e.Id)
            .FirstOrDefaultAsync();
    }

    private async Task<IActionResult> GetSalaryHistoryInternal(int employeeId, bool includeActor)
    {
        if (!await _db.Employees.AnyAsync(e => e.Id == employeeId))
            return NotFound(ApiResponse.Fail("Không tìm thấy nhân viên."));

        var rows = await _db.SalaryCoefficients
            .Include(sc => sc.CreatedByUser).ThenInclude(u => u.Employee)
            .Where(sc => sc.EmployeeId == employeeId)
            .OrderByDescending(sc => sc.EffectiveFrom)
            .ThenByDescending(sc => sc.CreatedAt)
            .ToListAsync();

        var history = rows.Select(sc => MapSalaryDto(sc, includeActor)).ToList();
        return Ok(ApiResponse<List<SalaryCoefficientDto>>.Ok(history));
    }

    private static SalaryCoefficientDto MapSalaryDto(SalaryCoefficient sc, bool includeActor)
    {
        var dto = new SalaryCoefficientDto
        {
            Id = sc.Id,
            EmployeeId = sc.EmployeeId,
            BaseSalaryPerHour = sc.BaseSalaryPerHour,
            Coefficient = sc.Coefficient,
            EffectiveFrom = sc.EffectiveFrom.ToString("yyyy-MM-dd"),
            Note = sc.Note,
            CreatedAt = sc.CreatedAt.ToString("yyyy-MM-dd HH:mm")
        };
        if (includeActor)
        {
            dto.CreatedByName = sc.CreatedByUser.Employee?.FullName ?? sc.CreatedByUser.Username;
            dto.CreatedByRole = sc.CreatedByUser.Role;
        }
        return dto;
    }

    private static EmployeeDto MapDto(Employee e)
    {
        var currentCoeff = e.SalaryCoefficients
            .OrderByDescending(sc => sc.EffectiveFrom)
            .FirstOrDefault(sc => sc.EffectiveFrom <= DateOnly.FromDateTime(DateTime.Today));

        return new EmployeeDto
        {
            Id = e.Id,
            UserId = e.UserId,
            EmployeeCode = e.EmployeeCode,
            FullName = e.FullName,
            DateOfBirth = e.DateOfBirth?.ToString("yyyy-MM-dd"),
            Gender = e.Gender,
            NationalId = e.NationalId,
            Phone = e.Phone,
            Address = e.Address,
            EmergencyContact = e.EmergencyContact,
            BankAccountNo = e.BankAccountNo,
            BankName = e.BankName,
            BankAccountName = e.BankAccountName,
            StartDate = e.StartDate.ToString("yyyy-MM-dd"),
            IsActive = e.IsActive,
            Role = e.User?.Role ?? "",
            Username = e.User?.Username ?? "",
            Email = e.User?.Email ?? "",
            StoreIds = e.EmployeeStores.Select(es => es.StoreId).ToList(),
            StoreNames = e.EmployeeStores.Select(es => es.Store?.Name ?? "").ToList(),
            CurrentSalary = currentCoeff == null ? null : new SalaryCoefficientDto
            {
                Id = currentCoeff.Id,
                EmployeeId = currentCoeff.EmployeeId,
                BaseSalaryPerHour = currentCoeff.BaseSalaryPerHour,
                Coefficient = currentCoeff.Coefficient,
                EffectiveFrom = currentCoeff.EffectiveFrom.ToString("yyyy-MM-dd"),
                Note = currentCoeff.Note,
                CreatedAt = currentCoeff.CreatedAt.ToString("yyyy-MM-dd HH:mm")
            }
        };
    }
}
