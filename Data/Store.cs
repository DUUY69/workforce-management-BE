namespace WorkforceManagement.Api.Data;

public class Store
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Address { get; set; }
    public string? Phone { get; set; }
    /// <summary>Giờ công chuẩn/ngày khi không có ca đăng ký (đọc từ DB).</summary>
    public decimal StandardWorkHoursPerDay { get; set; } = 8;
    /// <summary>Hệ số nhân lương phần OT (vd. 1.5 = 150%).</summary>
    public decimal OvertimeRateMultiplier { get; set; } = 1.5m;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public ICollection<EmployeeStore> EmployeeStores { get; set; } = new List<EmployeeStore>();
    public ICollection<Shift> Shifts { get; set; } = new List<Shift>();
    public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
    public ICollection<Payroll> Payrolls { get; set; } = new List<Payroll>();
}
