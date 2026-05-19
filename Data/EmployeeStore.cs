namespace WorkforceManagement.Api.Data;

public class EmployeeStore
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public int StoreId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Employee Employee { get; set; } = null!;
    public Store Store { get; set; } = null!;
}
