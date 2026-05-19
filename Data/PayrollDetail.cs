namespace WorkforceManagement.Api.Data;

public class PayrollDetail
{
    public int Id { get; set; }
    public int PayrollId { get; set; }
    public int EmployeeId { get; set; }
    public decimal WorkedDays { get; set; } = 0;
    public decimal WorkedHours { get; set; } = 0;
    public decimal OvertimeHours { get; set; } = 0;
    public decimal BaseSalaryPerHour { get; set; }
    public decimal Coefficient { get; set; }
    public decimal GrossSalary { get; set; }        // (giờ thường + OT×hệ số OT) × BaseSalaryPerHour × Coefficient
    public decimal Bonus { get; set; } = 0;
    public decimal Deduction { get; set; } = 0;
    public decimal NetSalary => GrossSalary + Bonus - Deduction;
    public string? Note { get; set; }

    // Navigation
    public Payroll Payroll { get; set; } = null!;
    public Employee Employee { get; set; } = null!;
}
