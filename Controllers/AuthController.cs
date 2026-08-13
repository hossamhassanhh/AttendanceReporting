using System.Security.Claims;
using AttendanceApp.Data;
using AttendanceApp.Models;
using AttendanceApp.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AttendanceApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IPasswordHasher<AppUser> _passwordHasher;

    public AuthController(
        IDbContextFactory<AppDbContext> factory,
        IPasswordHasher<AppUser> passwordHasher)
    {
        _factory = factory;
        _passwordHasher = passwordHasher;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Username and password are required" });

        using var db = await _factory.CreateDbContextAsync();
        var username = request.Username.Trim();
        var user = await db.AppUsers.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null || !user.IsActive || string.IsNullOrWhiteSpace(user.PasswordHash))
            return Unauthorized(new { error = "Invalid username or password" });

        var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
            return Unauthorized(new { error = "Invalid username or password" });

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        user.LastLoginAt = DateTime.Now;
        await db.SaveChangesAsync();
        await SignInAsync(user, request.RememberMe);
        return Ok(ToCurrentUser(user));
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(AuthConstants.Scheme);
        return Ok(new { message = "Signed out" });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var user = await FindCurrentUserAsync();
        if (user == null)
        {
            await HttpContext.SignOutAsync(AuthConstants.Scheme);
            return Unauthorized();
        }

        return Ok(ToCurrentUser(user));
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword)
            || string.IsNullOrWhiteSpace(request.NewPassword)
            || request.NewPassword.Length < 10)
        {
            return BadRequest(new { error = "The new password must contain at least 10 characters" });
        }

        var user = await FindCurrentUserAsync();
        if (user == null)
            return Unauthorized();

        var verification = _passwordHasher.VerifyHashedPassword(
            user, user.PasswordHash, request.CurrentPassword);
        if (verification == PasswordVerificationResult.Failed)
            return BadRequest(new { error = "Current password is incorrect" });

        user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword);
        user.MustChangePassword = false;
        user.SessionVersion++;
        using var db = await _factory.CreateDbContextAsync();
        db.AppUsers.Update(user);
        await db.SaveChangesAsync();
        await SignInAsync(user, false);
        return Ok(ToCurrentUser(user));
    }

    private async Task<AppUser?> FindCurrentUserAsync()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdValue, out var userId))
            return null;

        using var db = await _factory.CreateDbContextAsync();
        return await db.AppUsers.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
    }

    private async Task SignInAsync(AppUser user, bool persistent)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role),
            new("must_change_password", user.MustChangePassword ? "true" : "false"),
            new("session_version", user.SessionVersion.ToString())
        };
        claims.AddRange(GetPermissions(user).Select(
            permission => new Claim(AuthConstants.PermissionClaim, permission)));

        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(claims, AuthConstants.Scheme));
        await HttpContext.SignInAsync(
            AuthConstants.Scheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = persistent,
                AllowRefresh = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(persistent ? 12 : 4)
            });
    }

    internal static object ToCurrentUser(AppUser user)
    {
        var permissions = GetPermissions(user);
        return new
        {
            user.Id,
            user.Username,
            user.DisplayName,
            user.DisplayNameAr,
            user.DisplayNameEn,
            user.Role,
            Permissions = permissions,
            IsAdmin = string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase),
            user.MustChangePassword
        };
    }

    private static string[] GetPermissions(AppUser user) =>
        user.Permissions.Split(
                ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool RememberMe { get; set; }
}

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
