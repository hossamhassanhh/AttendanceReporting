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
    private const string AdSelfPermissions = "SelfAttendance,SelfLeave";

    internal const string SystemOwnerUsername = "4779";

    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IPasswordHasher<AppUser> _passwordHasher;
    private readonly AdDirectoryService _adDirectory;

    public AuthController(
        IDbContextFactory<AppDbContext> factory,
        IPasswordHasher<AppUser> passwordHasher,
        AdDirectoryService adDirectory)
    {
        _factory = factory;
        _passwordHasher = passwordHasher;
        _adDirectory = adDirectory;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Username and password are required" });

        using var db = await _factory.CreateDbContextAsync();
        var username = NormalizeUsername(request.Username);
        if (string.IsNullOrWhiteSpace(username))
            return BadRequest(new { error = "Username and password are required" });
        var user = await db.AppUsers.FirstOrDefaultAsync(u => u.Username == username);

        var localPasswordOk = false;
        if (user != null && user.IsActive && !string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
            if (verification == PasswordVerificationResult.Failed)
            {
                localPasswordOk = false;
            }
            else
            {
                localPasswordOk = true;
                if (verification == PasswordVerificationResult.SuccessRehashNeeded)
                    user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
            }
        }

        if (!localPasswordOk)
        {
            if (!await _adDirectory.ValidateCredentialsAsync(username, request.Password))
                return Unauthorized(new { error = "Invalid username or password" });

            user = await EnsureAdUserAsync(db, username);
            if (user == null)
                return Unauthorized(new { error = "Your Active Directory account is not linked to an employee record" });
            if (!user.IsActive)
                return Unauthorized(new { error = "Your account is disabled" });
        }

        if (!IsRoleSelectionAllowed(user!, request.Role))
            return Unauthorized(new { error = "This account is not authorized for the selected role" });

        user!.LastLoginAt = DateTime.Now;
        await db.SaveChangesAsync();

        var employeeNo = await GetEmployeeNoAsync(db, username);
        var session = ResolveSession(user, RequestedTier(request.Role));
        await SignInAsync(user, request.RememberMe, employeeNo, session.Role, session.Permissions, request.Role);
        return Ok(ToCurrentUser(user, employeeNo, session.Role, session.Permissions));
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

        using var db = await _factory.CreateDbContextAsync();
        var employeeNo = await GetEmployeeNoAsync(db, user.Username);
        var session = ResolveSession(user, SelectedSessionTier());
        return Ok(ToCurrentUser(user, employeeNo, session.Role, session.Permissions));
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
        var employeeNo = await GetEmployeeNoAsync(db, user.Username);
        var preservedRole = User.FindFirstValue("login_role");
        var session = ResolveSession(user, string.IsNullOrWhiteSpace(preservedRole) ? null : (int?)RequestedTier(preservedRole));
        await SignInAsync(user, false, employeeNo, session.Role, session.Permissions, preservedRole);
        return Ok(ToCurrentUser(user, employeeNo, session.Role, session.Permissions));
    }

    private async Task<AppUser?> EnsureAdUserAsync(AppDbContext db, string username)
    {
        var info = await _adDirectory.GetUserInfoAsync(username);
        if (info == null)
            return null;

        var isEmployee = await db.Employees.AnyAsync(e => e.FinancialNo == username);
        if (!isEmployee)
            return null;

        var isSystemOwner = string.Equals(username, SystemOwnerUsername, StringComparison.OrdinalIgnoreCase);
        var user = await db.AppUsers.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null)
        {
            user = new AppUser
            {
                Username = username,
                DisplayName = info.DisplayName,
                Role = isSystemOwner ? "SystemOwner" : "Employee",
                Permissions = isSystemOwner ? PermissionCatalog.AdminPermissions : AdSelfPermissions,
                PasswordHash = string.Empty,
                MustChangePassword = false,
                IsActive = true
            };
            db.AppUsers.Add(user);
        }
        else
        {
            if (!user.IsActive)
                return null;
            // Keep the stored display name (AppUsers data is authoritative for
            // existing accounts, e.g. manually set names must survive AD login).
            if (isSystemOwner && !string.Equals(user.Role, "SystemOwner", StringComparison.OrdinalIgnoreCase))
            {
                user.Role = "SystemOwner";
                user.Permissions = PermissionCatalog.AdminPermissions;
            }
        }

        return user;
    }

    private async Task<string?> GetEmployeeNoAsync(AppDbContext db, string username)
    {
        var exists = await db.Employees.AnyAsync(e => e.FinancialNo == username);
        return exists ? username : null;
    }

    private int? SelectedSessionTier()
    {
        var loginRole = User.FindFirstValue("login_role");
        return string.IsNullOrWhiteSpace(loginRole) ? null : RequestedTier(loginRole);
    }

    private async Task<AppUser?> FindCurrentUserAsync()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdValue, out var userId))
            return null;

        using var db = await _factory.CreateDbContextAsync();
        return await db.AppUsers.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
    }

    private async Task SignInAsync(
        AppUser user,
        bool persistent,
        string? employeeNo,
        string? effectiveRole = null,
        string[]? effectivePermissions = null,
        string? loginRole = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, effectiveRole ?? user.Role),
            new("must_change_password", user.MustChangePassword ? "true" : "false"),
            new("session_version", user.SessionVersion.ToString())
        };
        if (!string.IsNullOrWhiteSpace(loginRole))
            claims.Add(new Claim("login_role", loginRole.Trim()));
        if (!string.IsNullOrWhiteSpace(employeeNo))
            claims.Add(new Claim("employee_no", employeeNo));
        claims.AddRange((effectivePermissions ?? GetPermissions(user)).Select(
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

    internal static bool IsSystemOwner(AppUser user) =>
        string.Equals(user.Role, "SystemOwner", StringComparison.OrdinalIgnoreCase);

    internal static bool IsAdminRole(AppUser user) =>
        IsSystemOwner(user) || string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase);

    private static int RoleTier(AppUser user)
    {
        if (IsSystemOwner(user)) return 4;
        if (string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase)) return 3;
        if (string.Equals(user.Role, "Employee", StringComparison.OrdinalIgnoreCase)
            && GetPermissions(user).Any(p =>
                !string.Equals(p, "SelfAttendance", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(p, "SelfLeave", StringComparison.OrdinalIgnoreCase)))
            return 2;
        return 1;
    }

    private static bool IsRoleSelectionAllowed(AppUser user, string? requestedRole)
    {
        if (string.IsNullOrWhiteSpace(requestedRole))
            return true;
        return RoleTier(user) >= RequestedTier(requestedRole);
    }

    private static int RequestedTier(string? requestedRole)
    {
        if (string.IsNullOrWhiteSpace(requestedRole))
            return 1;
        return requestedRole.Trim().ToLowerInvariant() switch
        {
            "systemowner" => 4,
            "admin" => 3,
            "hr" or "hremployee" or "hr_employee" => 2,
            _ => 1
        };
    }

    private static string NormalizeUsername(string? username)
    {
        var value = (username ?? string.Empty).Trim();
        var at = value.IndexOf('@');
        if (at > 0)
            value = value[..at].Trim();
        var slash = value.LastIndexOf('\\');
        if (slash >= 0)
            value = value[(slash + 1)..].Trim();
        return value;
    }

    private static string[] SplitPermissions(string? permissions) =>
        (permissions ?? string.Empty).Split(
                ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static readonly string[] SelfServicePermissions = { "SelfAttendance", "SelfLeave" };

    private static (string Role, string[] Permissions) ResolveSession(AppUser user, int? selectedTier)
    {
        var entitledTier = RoleTier(user);
        if (selectedTier is null || selectedTier >= entitledTier)
            return (user.Role, GetPermissions(user));
        return selectedTier switch
        {
            3 => ("Admin", SplitPermissions(PermissionCatalog.AdminPermissions)),
            2 => ("Employee", SplitPermissions(PermissionCatalog.EmployeePermissions)),
            _ => ("Employee", SelfServicePermissions),
        };
    }

    internal static object ToCurrentUser(
        AppUser user,
        string? employeeNo = null,
        string? effectiveRole = null,
        string[]? effectivePermissions = null)
    {
        var role = effectiveRole ?? user.Role;
        var permissions = effectivePermissions ?? GetPermissions(user);
        var isSystemOwner = string.Equals(role, "SystemOwner", StringComparison.OrdinalIgnoreCase);
        var isAdmin = isSystemOwner || string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
        return new
        {
            user.Id,
            user.Username,
            user.DisplayName,
            user.DisplayNameAr,
            user.DisplayNameEn,
            Role = role,
            Permissions = permissions,
            IsAdmin = isAdmin,
            IsSystemOwner = isSystemOwner,
            EmployeeNo = employeeNo,
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
    public string? Role { get; set; }
}

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}