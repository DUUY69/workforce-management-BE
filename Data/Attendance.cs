using WorkforceManagement.Api.Services;

namespace WorkforceManagement.Api.Data;

public class Attendance
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public int StoreId { get; set; }
    public DateOnly WorkDate { get; set; }
    public TimeOnly CheckIn { get; set; }
    public TimeOnly? CheckOut { get; set; }
    public decimal OvertimeHours { get; set; } = 0;
    public string Status { get; set; } = "Worked"; // Worked | Absent
    public string? Note { get; set; }
    public int CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public bool IsOpen => CheckOut == null && Status == "Worked";

    // Computed (not mapped to DB column)
    public decimal WorkedHours =>
        CheckOut.HasValue
            ? AttendanceRules.CalcWorkedHours(CheckIn, CheckOut.Value)
            : 0;

    // Navigation
    public Employee Employee { get; set; } = null!;
    public Store Store { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
    public User? UpdatedByUser { get; set; }
}
