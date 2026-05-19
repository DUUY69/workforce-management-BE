using Microsoft.EntityFrameworkCore;
using WorkforceManagement.Api.Data;
using WorkforceManagement.Api.Models.Payroll;

namespace WorkforceManagement.Api.Services;

public class PayrollService
{
    private readonly AppDbContext _db;

    public PayrollService(AppDbContext db) => _db = db;

    /// <summary>Tạo hoặc tính lại bảng lương cho store + tháng. Chỉ hoạt động khi chưa có hoặc đang Draft.</summary>
    public async Task<PayrollDto> GenerateAsync(GeneratePayrollDto req, int createdBy)
    {
        var existing = await _db.Payrolls
            .Include(p => p.Details)
            .FirstOrDefaultAsync(p => p.StoreId == req.StoreId && p.Month == req.Month && p.Year == req.Year);

        if (existing != null && existing.Status != "Draft")
            throw new InvalidOperationException($"Bảng lương tháng {req.Month}/{req.Year} đã {existing.Status}, không thể tính lại.");

        var store = await _db.Stores.AsNoTracking().FirstOrDefaultAsync(s => s.Id == req.StoreId)
            ?? throw new InvalidOperationException($"Không tìm thấy cửa hàng Id={req.StoreId} trong DB.");
        if (store.StandardWorkHoursPerDay <= 0)
            throw new InvalidOperationException("Cửa hàng chưa cấu hình giờ công chuẩn/ngày trong DB.");

        // Chấm công tại cửa hàng này trong tháng (nguồn tính lương — không gắn 1 NV / 1 CH)
        var dateFrom = new DateOnly(req.Year, req.Month, 1);
        var dateTo = dateFrom.AddMonths(1).AddDays(-1);

        var attendances = await _db.Attendances
            .Where(a => a.StoreId == req.StoreId
                && a.WorkDate >= dateFrom && a.WorkDate <= dateTo
                && a.Status == AttendanceStatuses.Worked
                && a.CheckOut != null)
            .ToListAsync();

        var employeeIds = attendances.Select(a => a.EmployeeId).Distinct().ToList();
        if (employeeIds.Count == 0)
            throw new InvalidOperationException(
                $"Không có chấm công «Đi làm» đã ra ca tại cửa hàng này trong tháng {req.Month}/{req.Year}.");

        var employees = await _db.Employees
            .Where(e => employeeIds.Contains(e.Id) && e.IsActive)
            .ToListAsync();

        var coefficients = await _db.SalaryCoefficients
            .Where(sc => employeeIds.Contains(sc.EmployeeId) && sc.EffectiveFrom <= dateFrom)
            .OrderByDescending(sc => sc.EffectiveFrom)
            .ToListAsync();

        // Tạo hoặc reset payroll
        Payroll payroll;
        if (existing != null)
        {
            payroll = existing;
            _db.PayrollDetails.RemoveRange(existing.Details);
        }
        else
        {
            payroll = new Payroll
            {
                StoreId = req.StoreId,
                Month = req.Month,
                Year = req.Year,
                CreatedBy = createdBy
            };
            _db.Payrolls.Add(payroll);
            await _db.SaveChangesAsync();
        }

        // Tính lương từng nhân viên
        var details = new List<PayrollDetail>();
        foreach (var emp in employees)
        {
            var empAttendances = attendances.Where(a => a.EmployeeId == emp.Id).ToList();
            if (empAttendances.Count == 0) continue;

            var workedDays = empAttendances.Count;
            var workedHours = empAttendances.Sum(a => a.WorkedHours);
            var overtimeHours = empAttendances.Sum(a => a.OvertimeHours);

            // Lấy hệ số lương hiệu lực tại tháng này
            var coeff = coefficients.FirstOrDefault(sc => sc.EmployeeId == emp.Id);
            var hourlyRate = coeff?.BaseSalaryPerHour ?? 0;
            var coefficient = coeff?.Coefficient ?? 1.0m;
            var regularHours = Math.Max(0, workedHours - overtimeHours);
            var otRate = store.OvertimeRateMultiplier;
            // Lương = (giờ thường + OT × hệ số OT) × đơn giá/giờ × hệ số NV
            var grossSalary = Math.Round(
                (regularHours * hourlyRate + overtimeHours * hourlyRate * otRate) * coefficient, 0);

            details.Add(new PayrollDetail
            {
                PayrollId = payroll.Id,
                EmployeeId = emp.Id,
                WorkedDays = workedDays,
                WorkedHours = workedHours,
                OvertimeHours = overtimeHours,
                BaseSalaryPerHour = hourlyRate,
                Coefficient = coefficient,
                GrossSalary = grossSalary,
                Bonus = 0,
                Deduction = 0
            });
        }

        _db.PayrollDetails.AddRange(details);
        payroll.TotalAmount = details.Sum(d => d.GrossSalary + d.Bonus - d.Deduction);
        await _db.SaveChangesAsync();

        return await GetByIdAsync(payroll.Id);
    }

    public async Task<PayrollDto> GetByIdAsync(int id)
    {
        var p = await _db.Payrolls
            .Include(x => x.Store)
            .Include(x => x.Details).ThenInclude(d => d.Employee)
            .FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new KeyNotFoundException("Không tìm thấy bảng lương.");

        return MapDto(p);
    }

    public async Task<List<PayrollDto>> GetAllAsync(int? storeId, int? month, int? year, string? status)
    {
        var q = _db.Payrolls.Include(x => x.Store).Include(x => x.Details).ThenInclude(d => d.Employee).AsQueryable();
        if (storeId.HasValue) q = q.Where(x => x.StoreId == storeId.Value);
        if (month.HasValue) q = q.Where(x => x.Month == month.Value);
        if (year.HasValue) q = q.Where(x => x.Year == year.Value);
        if (!string.IsNullOrEmpty(status)) q = q.Where(x => x.Status == status);
        var list = await q.OrderByDescending(x => x.Year).ThenByDescending(x => x.Month).ToListAsync();
        return list.Select(MapDto).ToList();
    }

    public async Task<PayrollDto> ApproveAsync(int id, int approvedBy)
    {
        var p = await _db.Payrolls.FindAsync(id) ?? throw new KeyNotFoundException();
        if (p.Status != "Draft") throw new InvalidOperationException("Chỉ có thể duyệt bảng lương ở trạng thái Draft.");
        p.Status = "Approved";
        p.ApprovedBy = approvedBy;
        p.ApprovedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<PayrollDto> MarkPaidAsync(int id, int recordedBy)
    {
        var p = await _db.Payrolls
            .Include(x => x.Details).ThenInclude(d => d.Employee)
            .FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new KeyNotFoundException();

        if (p.Status != "Approved") throw new InvalidOperationException("Chỉ có thể đánh dấu đã trả khi bảng lương đã Approved.");

        p.Status = "Paid";

        // Tạo payment records
        foreach (var detail in p.Details)
        {
            _db.Payments.Add(new Payment
            {
                PayrollId = p.Id,
                EmployeeId = detail.EmployeeId,
                Amount = detail.NetSalary,
                PaymentDate = DateOnly.FromDateTime(DateTime.UtcNow),
                PaymentMethod = "Transfer",
                RecordedBy = recordedBy
            });
        }

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task UpdateDetailAsync(int payrollId, int detailId, UpdatePayrollDetailDto dto)
    {
        var p = await _db.Payrolls.FindAsync(payrollId) ?? throw new KeyNotFoundException();
        if (p.Status != "Draft") throw new InvalidOperationException("Chỉ có thể sửa bảng lương ở trạng thái Draft.");

        var detail = await _db.PayrollDetails.FindAsync(detailId) ?? throw new KeyNotFoundException();
        if (detail.PayrollId != payrollId) throw new InvalidOperationException("Detail không thuộc payroll này.");

        if (dto.Bonus.HasValue) detail.Bonus = dto.Bonus.Value;
        if (dto.Deduction.HasValue) detail.Deduction = dto.Deduction.Value;
        if (dto.Note != null) detail.Note = dto.Note;

        p.TotalAmount = await _db.PayrollDetails
            .Where(d => d.PayrollId == payrollId)
            .SumAsync(d => d.GrossSalary + d.Bonus - d.Deduction);

        await _db.SaveChangesAsync();
    }

    private static PayrollDto MapDto(Payroll p) => new()
    {
        Id = p.Id,
        StoreId = p.StoreId,
        StoreName = p.Store?.Name ?? "",
        Month = p.Month,
        Year = p.Year,
        Status = p.Status,
        TotalAmount = p.TotalAmount,
        CreatedAt = p.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
        ApprovedAt = p.ApprovedAt?.ToString("yyyy-MM-dd HH:mm"),
        Details = p.Details.Select(d => new PayrollDetailDto
        {
            Id = d.Id,
            EmployeeId = d.EmployeeId,
            EmployeeName = d.Employee?.FullName ?? "",
            EmployeeCode = d.Employee?.EmployeeCode ?? "",
            WorkedDays = d.WorkedDays,
            WorkedHours = d.WorkedHours,
            OvertimeHours = d.OvertimeHours,
            BaseSalaryPerHour = d.BaseSalaryPerHour,
            Coefficient = d.Coefficient,
            GrossSalary = d.GrossSalary,
            Bonus = d.Bonus,
            Deduction = d.Deduction,
            NetSalary = d.NetSalary,
            Note = d.Note
        }).ToList()
    };
}
