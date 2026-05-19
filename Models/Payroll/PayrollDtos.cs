namespace WorkforceManagement.Api.Models.Payroll;

public class PayrollDto
{
    public int Id { get; set; }
    public int StoreId { get; set; }
    public string StoreName { get; set; } = "";
    public int Month { get; set; }
    public int Year { get; set; }
    public string Status { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public string CreatedAt { get; set; } = "";
    public string? ApprovedAt { get; set; }
    public List<PayrollDetailDto> Details { get; set; } = new();
}

public class PayrollDetailDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = "";
    public string EmployeeCode { get; set; } = "";
    public string? BankAccountNo { get; set; }
    public string? BankName { get; set; }
    public string? BankAccountName { get; set; }
    public decimal WorkedDays { get; set; }
    public decimal WorkedHours { get; set; }
    public decimal OvertimeHours { get; set; }
    public decimal BaseSalaryPerHour { get; set; }
    public decimal Coefficient { get; set; }
    public decimal GrossSalary { get; set; }
    public decimal Bonus { get; set; }
    public decimal Deduction { get; set; }
    public decimal NetSalary { get; set; }
    public string? Note { get; set; }
}

public class GeneratePayrollDto
{
    public int StoreId { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
}

public class UpdatePayrollDetailDto
{
    public decimal? Bonus { get; set; }
    public decimal? Deduction { get; set; }
    public string? Note { get; set; }
}

public class PaymentDto
{
    public int Id { get; set; }
    public int PayrollId { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = "";
    public decimal Amount { get; set; }
    public string PaymentDate { get; set; } = "";
    public string? PaymentMethod { get; set; }
    public string? Note { get; set; }
    public string RecordedByName { get; set; } = "";
    public string CreatedAt { get; set; } = "";
}
