using System.Text.Json.Serialization;

namespace WorkforceManagement.Api.Models.Employee;

public class EmployeeDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string EmployeeCode { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? NationalId { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? EmergencyContact { get; set; }
    public string? BankAccountNo { get; set; }
    public string? BankName { get; set; }
    public string? BankAccountName { get; set; }
    public string StartDate { get; set; } = "";
    public bool IsActive { get; set; }
    public string Role { get; set; } = "";
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public List<int> StoreIds { get; set; } = new();
    public List<string> StoreNames { get; set; } = new();
    public SalaryCoefficientDto? CurrentSalary { get; set; }
}

public class CreateEmployeeDto
{
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string Role { get; set; } = "Employee";
    public string? Phone { get; set; }
    public string? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? NationalId { get; set; }
    public string? Address { get; set; }
    public string? BankAccountNo { get; set; }
    public string? BankName { get; set; }
    public string? BankAccountName { get; set; }
    public string StartDate { get; set; } = "";
    public List<int> StoreIds { get; set; } = new();
    public decimal? BaseSalaryPerHour { get; set; }
    public decimal? Coefficient { get; set; }
}

public class UpdateEmployeeBankDto
{
    public string? BankAccountNo { get; set; }
    public string? BankName { get; set; }
    public string? BankAccountName { get; set; }
}

public class UpdateEmployeeDto
{
    public string FullName { get; set; } = "";
    public string? Phone { get; set; }
    public string? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? NationalId { get; set; }
    public string? Address { get; set; }
    public string? EmergencyContact { get; set; }
    public string? BankAccountNo { get; set; }
    public string? BankName { get; set; }
    public string? BankAccountName { get; set; }
    public List<int> StoreIds { get; set; } = new();
}

public class SalaryCoefficientDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public decimal BaseSalaryPerHour { get; set; }
    /// <summary>Alias JSON cho client cũ (cùng giá trị với BaseSalaryPerHour).</summary>
    [JsonPropertyName("baseSalaryPerDay")]
    public decimal BaseSalaryPerDay { get => BaseSalaryPerHour; set => BaseSalaryPerHour = value; }
    public decimal Coefficient { get; set; }
    public string EffectiveFrom { get; set; } = "";
    public string? Note { get; set; }
    public string CreatedAt { get; set; } = "";
    /// <summary>Chỉ trả về cho Admin/Quản lý khi xem lịch sử NV.</summary>
    public string? CreatedByName { get; set; }
    public string? CreatedByRole { get; set; }
}

public class CreateSalaryCoefficientDto
{
    public int EmployeeId { get; set; }
    public decimal BaseSalaryPerHour { get; set; }
    public decimal Coefficient { get; set; } = 1.0m;
    public string? EffectiveFrom { get; set; } // Bỏ trống = ngày 1 tháng sau
    public string? Note { get; set; }
}
