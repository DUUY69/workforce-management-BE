namespace WorkforceManagement.Api.Data;

public class Shift
{
    public int Id { get; set; }
    public int StoreId { get; set; }
    public string Name { get; set; } = ""; // Ca sáng, Ca chiều, Ca tối
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Store Store { get; set; } = null!;
    public ICollection<ShiftRegistration> ShiftRegistrations { get; set; } = new List<ShiftRegistration>();
}
