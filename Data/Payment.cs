namespace WorkforceManagement.Api.Data;

public class Payment
{
    public int Id { get; set; }
    public int PayrollId { get; set; }
    public int EmployeeId { get; set; }
    public decimal Amount { get; set; }
    public DateOnly PaymentDate { get; set; }
    public string? PaymentMethod { get; set; } // Cash | Transfer
    public string? Note { get; set; }
    public int RecordedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Payroll Payroll { get; set; } = null!;
    public Employee Employee { get; set; } = null!;
    public User RecordedByUser { get; set; } = null!;
}
