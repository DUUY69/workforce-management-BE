using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkforceManagement.Api.Data;
using WorkforceManagement.Api.Models;
using WorkforceManagement.Api.Models.Payroll;
using WorkforceManagement.Api.Services;

namespace WorkforceManagement.Api.Controllers;

[ApiController]
[Route("api/payrolls")]
[Authorize]
public class PayrollsController : ControllerBase
{
    private readonly PayrollService _svc;
    private readonly AppDbContext _db;
    public PayrollsController(PayrollService svc, AppDbContext db) { _svc = svc; _db = db; }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
    private int? EmployeeId
    {
        get
        {
            var raw = User.FindFirstValue("employeeId");
            return int.TryParse(raw, out var id) && id > 0 ? id : null;
        }
    }

    /// <summary>Employee xem bảng lương cá nhân</summary>
    [HttpGet("my")]
    [Authorize(Roles = "Employee")]
    public async Task<IActionResult> GetMy()
    {
        if (EmployeeId == null) return Ok(ApiResponse<List<PayrollDto>>.Ok(new()));
        var details = await _db.PayrollDetails
            .Include(d => d.Payroll).ThenInclude(p => p.Store)
            .Include(d => d.Employee)
            .Where(d => d.EmployeeId == EmployeeId.Value)
            .OrderByDescending(d => d.Payroll.Year).ThenByDescending(d => d.Payroll.Month)
            .ToListAsync();

        var result = details.Select(d => new PayrollDto
        {
            Id = d.PayrollId,
            StoreId = d.Payroll.StoreId,
            StoreName = d.Payroll.Store?.Name ?? "",
            Month = d.Payroll.Month,
            Year = d.Payroll.Year,
            Status = d.Payroll.Status,
            TotalAmount = d.NetSalary,
            CreatedAt = d.Payroll.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
            Details = new List<PayrollDetailDto> {
                new() {
                    Id = d.Id, EmployeeId = d.EmployeeId,
                    EmployeeName = d.Employee?.FullName ?? "",
                    EmployeeCode = d.Employee?.EmployeeCode ?? "",
                    WorkedDays = d.WorkedDays, WorkedHours = d.WorkedHours,
                    BaseSalaryPerHour = d.BaseSalaryPerHour, Coefficient = d.Coefficient,
                    GrossSalary = d.GrossSalary, Bonus = d.Bonus, Deduction = d.Deduction,
                    NetSalary = d.NetSalary
                }
            }
        }).ToList();
        return Ok(ApiResponse<List<PayrollDto>>.Ok(result));
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? storeId, [FromQuery] int? month,
        [FromQuery] int? year, [FromQuery] string? status)
    {
        var scope = new UserStoreScope(_db, User);
        if (storeId.HasValue && !await scope.IsStoreFilterAllowedAsync(storeId))
            return StatusCode(403, ApiResponse.Fail("Không có quyền xem cửa hàng này."));

        var list = await _svc.GetAllAsync(storeId, month, year, status);
        var managedStoreIds = await scope.GetManagedStoreIdsAsync();
        if (managedStoreIds != null)
            list = list.Where(p => managedStoreIds.Contains(p.StoreId)).ToList();
        return Ok(ApiResponse<List<PayrollDto>>.Ok(list));
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var dto = await _svc.GetByIdAsync(id);
            var scope = new UserStoreScope(_db, User);
            if (!await scope.CanAccessStoreAsync(dto.StoreId))
                return StatusCode(403, ApiResponse.Fail("Không có quyền xem bảng lương này."));
            return Ok(ApiResponse<PayrollDto>.Ok(dto));
        }
        catch (KeyNotFoundException) { return NotFound(ApiResponse.Fail("Không tìm thấy bảng lương.")); }
    }

    [HttpPost("generate")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Generate([FromBody] GeneratePayrollDto dto)
    {
        var scope = new UserStoreScope(_db, User);
        if (!await scope.CanAccessStoreAsync(dto.StoreId))
            return StatusCode(403, ApiResponse.Fail("Không có quyền tính lương cửa hàng này."));

        try
        {
            var result = await _svc.GenerateAsync(dto, CurrentUserId);
            return Ok(ApiResponse<PayrollDto>.Ok(result, "Tạo bảng lương thành công."));
        }
        catch (InvalidOperationException ex) { return BadRequest(ApiResponse.Fail(ex.Message)); }
    }

    [HttpPatch("{id:int}/approve")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Approve(int id)
    {
        if (!await CanAccessPayrollAsync(id)) return PayrollForbidden();
        try { return Ok(ApiResponse<PayrollDto>.Ok(await _svc.ApproveAsync(id, CurrentUserId), "Đã duyệt bảng lương.")); }
        catch (Exception ex) { return BadRequest(ApiResponse.Fail(ex.Message)); }
    }

    [HttpPatch("{id:int}/pay")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> MarkPaid(int id)
    {
        if (!await CanAccessPayrollAsync(id)) return PayrollForbidden();
        try { return Ok(ApiResponse<PayrollDto>.Ok(await _svc.MarkPaidAsync(id, CurrentUserId), "Đã đánh dấu đã trả lương.")); }
        catch (Exception ex) { return BadRequest(ApiResponse.Fail(ex.Message)); }
    }

    [HttpPut("{id:int}/details/{detailId:int}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> UpdateDetail(int id, int detailId, [FromBody] UpdatePayrollDetailDto dto)
    {
        if (!await CanAccessPayrollAsync(id)) return PayrollForbidden();
        try { await _svc.UpdateDetailAsync(id, detailId, dto); return Ok(ApiResponse.Ok("Cập nhật thành công.")); }
        catch (Exception ex) { return BadRequest(ApiResponse.Fail(ex.Message)); }
    }

    private async Task<bool> CanAccessPayrollAsync(int payrollId)
    {
        var storeId = await _db.Payrolls.Where(p => p.Id == payrollId).Select(p => p.StoreId).FirstOrDefaultAsync();
        if (storeId == 0) return false;
        var scope = new UserStoreScope(_db, User);
        return await scope.CanAccessStoreAsync(storeId);
    }

    private static IActionResult PayrollForbidden() =>
        new ObjectResult(ApiResponse.Fail("Không có quyền thao tác bảng lương này.")) { StatusCode = 403 };
}
