namespace WorkforceManagement.Api.Data;

public class Payroll
{
    public int Id { get; set; }
    public int StoreId { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
    public string Status { get; set; } = "Draft"; // Draft | Approved | Paid
    public decimal TotalAmount { get; set; } = 0;
    public int CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }

    // Navigation
    public Store Store { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
    public User? ApprovedByUser { get; set; }
    public ICollection<PayrollDetail> Details { get; set; } = new List<PayrollDetail>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
