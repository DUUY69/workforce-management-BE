namespace WorkforceManagement.Api.Data;

public class SalaryCoefficient
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public decimal BaseSalaryPerHour { get; set; }  // Lương cơ bản/giờ (VNĐ)
    public decimal Coefficient { get; set; } = 1.0m; // Hệ số nhân
    public DateOnly EffectiveFrom { get; set; }      // Ngày 1 của tháng áp dụng
    public string? Note { get; set; }
    public int CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Employee Employee { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
}
