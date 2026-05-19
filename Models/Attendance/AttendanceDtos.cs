namespace WorkforceManagement.Api.Models.Attendance;

public class AttendanceDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = "";
    public string EmployeeCode { get; set; } = "";
    public int StoreId { get; set; }
    public string StoreName { get; set; } = "";
    public string WorkDate { get; set; } = "";
    public string CheckIn { get; set; } = "";
    public string? CheckOut { get; set; }
    public bool IsOpen { get; set; }
    public decimal WorkedHours { get; set; }
    public decimal OvertimeHours { get; set; }
    public decimal StandardHours { get; set; }
    public string Status { get; set; } = "";
    public string StatusLabel { get; set; } = "";
    public string? Note { get; set; }
    public string CreatedAt { get; set; } = "";
}

public class CreateAttendanceDto
{
    public int EmployeeId { get; set; }
    public int StoreId { get; set; }
    public string WorkDate { get; set; } = "";
    public string CheckIn { get; set; } = "";
    public string CheckOut { get; set; } = "";
    public string? Note { get; set; }
}

public class UpdateAttendanceDto
{
    public string CheckIn { get; set; } = "";
    public string CheckOut { get; set; } = "";
    public string? Note { get; set; }
}

public class CheckInDto
{
    public int StoreId { get; set; }
    public string? WorkDate { get; set; }
}

public class MarkAbsentDto
{
    public int EmployeeId { get; set; }
    public int StoreId { get; set; }
    public string WorkDate { get; set; } = "";
    public string? Note { get; set; }
}

public class AttendanceSummaryDto
{
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = "";
    public string EmployeeCode { get; set; } = "";
    public int WorkedDays { get; set; }
    public decimal WorkedHours { get; set; }
    public decimal OvertimeHours { get; set; }
}
