using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkforceManagement.Api.Models;
using WorkforceManagement.Api.Models.Auth;
using WorkforceManagement.Api.Services;

namespace WorkforceManagement.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;

    public AuthController(AuthService auth) => _auth = auth;

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var result = await _auth.LoginAsync(req);
        if (result == null)
            return Unauthorized(ApiResponse.Fail("Tên đăng nhập hoặc mật khẩu không đúng."));
        return Ok(ApiResponse<LoginResponse>.Ok(result, "Đăng nhập thành công."));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req)
    {
        var result = await _auth.RefreshAsync(req.RefreshToken);
        if (result == null)
            return Unauthorized(ApiResponse.Fail("Refresh token không hợp lệ hoặc đã hết hạn."));
        return Ok(ApiResponse<LoginResponse>.Ok(result));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest req)
    {
        await _auth.RevokeAsync(req.RefreshToken);
        return Ok(ApiResponse.Ok("Đăng xuất thành công."));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me([FromServices] Data.AppDbContext db)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        var user = await db.Users.Include(u => u.Employee).FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return Unauthorized(ApiResponse.Fail("Phiên đăng nhập không hợp lệ."));

        return Ok(ApiResponse<object>.Ok(new
        {
            id = user.Id,
            username = user.Username,
            email = user.Email,
            role = user.Role,
            fullName = user.Employee?.FullName ?? user.Username,
            employeeId = user.Employee?.Id,
            employeeCode = user.Employee?.EmployeeCode,
            bankAccountNo = user.Employee?.BankAccountNo,
            bankName = user.Employee?.BankName,
            bankAccountName = user.Employee?.BankAccountName,
        }));
    }
}
