using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkforceManagement.Api.Data;
using WorkforceManagement.Api.Models;

namespace WorkforceManagement.Api.Controllers;

/// <summary>Khởi tạo hệ thống — không seed dữ liệu nghiệp vụ trong API. Dùng script trong Database/.</summary>
[ApiController]
[Route("api/setup")]
public class SetupController : ControllerBase
{
    private readonly AppDbContext _db;

    public SetupController(AppDbContext db) => _db = db;

    /// <summary>Trạng thái DB (để biết đã chạy script seed SQL chưa).</summary>
    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        var counts = new
        {
            users = await _db.Users.CountAsync(),
            stores = await _db.Stores.CountAsync(),
            employees = await _db.Employees.CountAsync(),
            attendances = await _db.Attendances.CountAsync(),
            payrolls = await _db.Payrolls.CountAsync(),
        };
        var ready = counts.users > 0 && counts.stores > 0;
        var message = ready
            ? "DB đã có dữ liệu. Quản lý qua app hoặc script SQL."
            : "DB trống. Chạy Database/postgres/01_schema.sql rồi 02_seed.sql (PostgreSQL).";
        return Ok(ApiResponse<object>.Ok(new { counts, ready }, message));
    }

    /// <summary>Đặt lại mật khẩu demo sau seed SQL (BCrypt thật).</summary>
    [HttpPost("reset-demo-passwords")]
    public async Task<IActionResult> ResetDemoPasswords()
    {
        var map = new Dictionary<string, string>
        {
            ["admin"] = "Admin@123",
            ["manager1"] = "Manager@123",
            ["nv001"] = "Employee@123",
            ["nv002"] = "Employee@123",
            ["nv003"] = "Employee@123",
        };

        var updated = new List<string>();
        foreach (var (username, password) in map)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null) continue;
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
            updated.Add(username);
        }

        await _db.SaveChangesAsync();
        return Ok(ApiResponse.Ok($"Đã cập nhật BCrypt cho: {string.Join(", ", updated)}"));
    }

    /// <summary>Tạo tài khoản Admin đầu tiên (khi chưa có admin).</summary>
    [HttpPost("admin")]
    public async Task<IActionResult> CreateAdmin([FromBody] CreateAdminDto dto)
    {
        if (await _db.Users.AnyAsync(u => u.Role == "Admin"))
            return BadRequest(ApiResponse.Fail("Admin đã tồn tại."));

        var user = new User
        {
            Username = dto.Username,
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = "Admin"
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse.Ok($"Tạo admin '{dto.Username}' thành công."));
    }
}

public class CreateAdminDto
{
    public string Username { get; set; } = "admin";
    public string Email { get; set; } = "admin@workforce.vn";
    public string Password { get; set; } = "";
}
