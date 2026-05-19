namespace WorkforceManagement.Api.Models.Shift;

public class ShiftDto
{
    public int Id { get; set; }
    public int StoreId { get; set; }
    public string StoreName { get; set; } = "";
    public string Name { get; set; } = "";
    public string StartTime { get; set; } = ""; // "08:00"
    public string EndTime { get; set; } = "";
    public double WorkHours { get; set; }
    public bool IsActive { get; set; }
}

public class CreateShiftDto
{
    public int StoreId { get; set; }
    public string Name { get; set; } = "";
    public string StartTime { get; set; } = "";
    public string EndTime { get; set; } = "";
}

public class UpdateShiftDto
{
    public string Name { get; set; } = "";
    public string StartTime { get; set; } = "";
    public string EndTime { get; set; } = "";
}

public class ShiftRegistrationDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = "";
    public int? ShiftId { get; set; }
    public string ShiftName { get; set; } = "";
    public string StartTime { get; set; } = "";
    public string EndTime { get; set; } = "";
    public string ShiftTime { get; set; } = ""; // "08:00 - 12:00"
    public int StoreId { get; set; }
    public string StoreName { get; set; } = "";
    public string WorkDate { get; set; } = "";
    public string Status { get; set; } = "";
    public string? RejectReason { get; set; }
    public string CreatedAt { get; set; } = "";
}

public class CreateShiftRegistrationDto
{
    public int StoreId { get; set; }
    public string WorkDate { get; set; } = ""; // yyyy-MM-dd
    public string StartTime { get; set; } = ""; // HH:mm
    public string EndTime { get; set; } = "";
}

public class ReviewShiftRegistrationDto
{
    public string? RejectReason { get; set; }
}
