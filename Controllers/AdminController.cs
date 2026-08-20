using AttendanceApp.Data;
using AttendanceApp.Models;
using AttendanceApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AttendanceApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AdminController : ControllerBase
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IPasswordHasher<AppUser> _passwordHasher;
    private readonly AdDirectoryService _adDirectory;

    public AdminController(
        IDbContextFactory<AppDbContext> factory,
        IPasswordHasher<AppUser> passwordHasher,
        AdDirectoryService adDirectory)
    {
        _factory = factory;
        _passwordHasher = passwordHasher;
        _adDirectory = adDirectory;
    }

    [HttpGet("permissions")]
    public IActionResult GetPermissions()
    {
        return Ok(new
        {
            permissions = PermissionCatalog.All,
            admin = PermissionCatalog.AdminPermissions.Split(','),
            employee = PermissionCatalog.EmployeePermissions.Split(',')
        });
    }

    [HttpGet("current-user")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(username))
            return Unauthorized();

        using var db = await _factory.CreateDbContextAsync();
        var user = await db.AppUsers.FirstOrDefaultAsync(u => u.Username == username && u.IsActive);

        if (user == null)
            return NotFound(new { error = "Current user was not found" });

        return Ok(AuthController.ToCurrentUser(user));
    }

    [Authorize(Policy = "ManagePermissions")]
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        using var db = await _factory.CreateDbContextAsync();
        var users = await db.AppUsers.AsNoTracking().OrderBy(u => u.Username).ToListAsync();
        return Ok(users.Select(ToSafeUser));
    }

    [Authorize(Policy = "CreateUsers")]
    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] SaveUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest(new { error = "Username is required" });
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 10)
            return BadRequest(new { error = "A temporary password of at least 10 characters is required" });

        using var db = await _factory.CreateDbContextAsync();
        var username = request.Username.Trim();
        if (await db.AppUsers.AnyAsync(u => u.Username == username))
            return Conflict(new { error = "User already exists" });

        var role = NormalizeRole(request.Role);
        var user = new AppUser
        {
            Username = username,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? username : request.DisplayName.Trim(),
            DisplayNameAr = string.IsNullOrWhiteSpace(request.DisplayNameAr) ? null : request.DisplayNameAr.Trim(),
            DisplayNameEn = string.IsNullOrWhiteSpace(request.DisplayNameEn) ? null : request.DisplayNameEn.Trim(),
            Role = role,
            Permissions = NormalizePermissions(role, request.Permissions),
            IsActive = request.IsActive ?? true,
            MustChangePassword = true
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        db.AppUsers.Add(user);
        await db.SaveChangesAsync();
        return Ok(ToSafeUser(user));
    }

    [Authorize(Policy = "ManagePermissions")]
    [HttpPost("users/{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 10)
            return BadRequest(new { error = "The temporary password must contain at least 10 characters" });

        using var db = await _factory.CreateDbContextAsync();
        var user = await db.AppUsers.FindAsync(id);
        if (user == null)
            return NotFound(new { error = "User was not found" });

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
        user.MustChangePassword = true;
        user.SessionVersion++;
        await db.SaveChangesAsync();
        return Ok(new { message = "Password reset", user = ToSafeUser(user) });
    }

    [Authorize(Policy = "ManagePermissions")]
    [HttpPut("users/{id:int}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] SaveUserRequest request)
    {
        using var db = await _factory.CreateDbContextAsync();
        var user = await db.AppUsers.FindAsync(id);
        if (user == null) return NotFound();

        var role = NormalizeRole(request.Role);
        user.DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? user.Username : request.DisplayName.Trim();
        user.DisplayNameAr = string.IsNullOrWhiteSpace(request.DisplayNameAr) ? user.DisplayNameAr : request.DisplayNameAr.Trim();
        user.DisplayNameEn = string.IsNullOrWhiteSpace(request.DisplayNameEn) ? user.DisplayNameEn : request.DisplayNameEn.Trim();
        user.Role = role;
        user.Permissions = NormalizePermissions(role, request.Permissions);
        user.IsActive = request.IsActive ?? user.IsActive;
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            if (request.Password.Length < 10)
                return BadRequest(new { error = "The temporary password must contain at least 10 characters" });
            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
            user.MustChangePassword = true;
        }
        user.SessionVersion++;
        await db.SaveChangesAsync();
        return Ok(ToSafeUser(user));
    }

    [Authorize(Policy = "ManageCalendar")]
    [HttpGet("day-settings")]
    public async Task<IActionResult> GetDaySettings([FromQuery] int? year, [FromQuery] int? month)
    {
        using var db = await _factory.CreateDbContextAsync();
        var query = db.AttendanceDaySettings.AsNoTracking();
        if (year.HasValue && month.HasValue)
        {
            var start = new DateTime(year.Value, month.Value, 1);
            var end = start.AddMonths(1);
            query = query.Where(s => s.Date >= start && s.Date < end);
        }
        else if (year.HasValue)
        {
            var start = new DateTime(year.Value, 1, 1);
            var end = start.AddYears(1);
            query = query.Where(s => s.Date >= start && s.Date < end);
        }
        else if (month.HasValue)
        {
            query = query.Where(s => s.Date.Month == month.Value);
        }
        var settings = await query.OrderBy(s => s.Date).ToListAsync();
        return Ok(settings);
    }

    [Authorize(Policy = "ManageCalendar")]
    [HttpPost("day-settings")]
    public async Task<IActionResult> SaveDaySetting([FromBody] SaveDaySettingRequest request)
    {
        if (!AttendanceCalendarRules.DayTypes.Contains(request.DayType))
            return BadRequest(new { error = "Invalid day type" });

        using var db = await _factory.CreateDbContextAsync();
        var date = request.Date.Date;
        var setting = await db.AttendanceDaySettings.FirstOrDefaultAsync(s => s.Date == date);
        if (setting == null)
        {
            setting = new AttendanceDaySetting { Date = date };
            db.AttendanceDaySettings.Add(setting);
        }

        setting.DayType = request.DayType;
        setting.Notes = request.Notes;
        await db.SaveChangesAsync();
        return Ok(setting);
    }

    [Authorize(Policy = "ManageCalendar")]
    [HttpDelete("day-settings/{id:int}")]
    public async Task<IActionResult> DeleteDaySetting(int id)
    {
        using var db = await _factory.CreateDbContextAsync();
        var setting = await db.AttendanceDaySettings.FindAsync(id);
        if (setting == null) return NotFound();
        db.AttendanceDaySettings.Remove(setting);
        await db.SaveChangesAsync();
        return Ok(new { message = "Deleted" });
    }

    [Authorize(Policy = "ManageCalendar")]
    [HttpPut("employee-schedules/{financialNo}")]
    public async Task<IActionResult> UpdateEmployeeSchedule(
        string financialNo,
        [FromBody] SaveEmployeeScheduleRequest request)
    {
        if (!TryNormalizeSchedule(request.StartTime, request.EndTime, out var start, out var end, out var error))
            return BadRequest(new { error });

        using var db = await _factory.CreateDbContextAsync();
        var employee = await db.Employees.FindAsync(financialNo.Trim());
        if (employee == null)
            return NotFound(new { error = "Employee was not found" });

        employee.ScheduleStart = start;
        employee.ScheduleEnd = end;
        await db.SaveChangesAsync();
        return Ok(new
        {
            employee.FinancialNo,
            employee.Name,
            employee.ScheduleStart,
            employee.ScheduleEnd
        });
    }

    [Authorize(Policy = "ManageCalendar")]
    [HttpPut("employee-schedules")]
    public async Task<IActionResult> BulkUpdateEmployeeSchedules(
        [FromBody] BulkEmployeeScheduleRequest request)
    {
        if (!TryNormalizeSchedule(request.StartTime, request.EndTime, out var start, out var end, out var error))
            return BadRequest(new { error });

        var financialNumbers = request.FinancialNumbers
            .Select(value => value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (financialNumbers.Count == 0)
            return BadRequest(new { error = "At least one financial number is required" });

        using var db = await _factory.CreateDbContextAsync();
        var employees = await db.Employees
            .Where(employee => financialNumbers.Contains(employee.FinancialNo))
            .ToListAsync();
        foreach (var employee in employees)
        {
            employee.ScheduleStart = start;
            employee.ScheduleEnd = end;
        }

        await db.SaveChangesAsync();
        var found = employees.Select(employee => employee.FinancialNo).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Ok(new
        {
            updatedCount = employees.Count,
            missingFinancialNumbers = financialNumbers.Where(number => !found.Contains(number)).ToList()
        });
    }

    private static bool TryNormalizeSchedule(
        string? startValue,
        string? endValue,
        out string? start,
        out string? end,
        out string? error)
    {
        start = string.IsNullOrWhiteSpace(startValue) ? null : startValue.Trim();
        end = string.IsNullOrWhiteSpace(endValue) ? null : endValue.Trim();
        error = null;

        if (start == null && end == null)
            return true;
        if (!TimeOnly.TryParse(start, out var startTime) || !TimeOnly.TryParse(end, out var endTime))
        {
            error = "Start and end times must use HH:mm format";
            return false;
        }
        if (endTime <= startTime)
        {
            error = "Schedule end must be after schedule start";
            return false;
        }

        start = startTime.ToString("HH:mm");
        end = endTime.ToString("HH:mm");
        return true;
    }

    [Authorize(Policy = "AdSync")]
    [HttpPost("ad-sync")]
    public async Task<IActionResult> RunAdSync()
    {
        try
        {
            var result = await _adDirectory.SyncUsersAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [Authorize(Policy = "AdSync")]
    [HttpGet("ad-sync-status")]
    public async Task<IActionResult> GetAdSyncStatus()
    {
        var status = await _adDirectory.GetSyncStatusAsync();
        return Ok(status ?? new { });
    }

    [Authorize(Policy = "Employees")]
    [HttpPut("employees/{financialNo}/manager")]
    public async Task<IActionResult> UpdateEmployeeManager(
        string financialNo,
        [FromBody] SaveEmployeeManagerRequest request)
    {
        using var db = await _factory.CreateDbContextAsync();
        var employee = await db.Employees.FindAsync(financialNo.Trim());
        if (employee == null)
            return NotFound(new { error = "Employee was not found" });

        if (string.IsNullOrWhiteSpace(request.ManagerFinancialNo))
        {
            employee.ManagerFinancialNo = null;
        }
        else
        {
            var managerNo = request.ManagerFinancialNo.Trim();
            var managerExists = await db.Employees.AnyAsync(e => e.FinancialNo == managerNo);
            if (!managerExists)
                return BadRequest(new { error = "The manager financial number does not match an existing employee" });
            employee.ManagerFinancialNo = managerNo;
        }

        await db.SaveChangesAsync();
        return Ok(new
        {
            employee.FinancialNo,
            employee.Name,
            employee.ManagerFinancialNo
        });
    }

    private static string NormalizeRole(string? role)
    {
        return string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ? "Admin" : "Employee";
    }

    private static string NormalizePermissions(string role, IReadOnlyCollection<string>? permissions)
    {
        if (role == "Admin") return PermissionCatalog.AdminPermissions;
        var allowed = PermissionCatalog.All.Where(p => p != "CreateUsers").ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selected = permissions == null || permissions.Count == 0 ? allowed : permissions.Where(allowed.Contains).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return string.Join(',', selected.OrderBy(p => p));
    }

    private static object ToSafeUser(AppUser user) => new
    {
        user.Id,
        user.Username,
        user.DisplayName,
        user.DisplayNameAr,
        user.DisplayNameEn,
        user.Role,
        user.Permissions,
        user.IsActive,
        user.MustChangePassword,
        user.LastLoginAt,
        user.CreatedAt
    };
}

public class SaveUserRequest
{
    public string Username { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? DisplayNameAr { get; set; }
    public string? DisplayNameEn { get; set; }
    public string? Role { get; set; }
    public string? Password { get; set; }
    public List<string>? Permissions { get; set; }
    public bool? IsActive { get; set; }
}

public class SaveDaySettingRequest
{
    public DateTime Date { get; set; }
    public string DayType { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class ResetPasswordRequest
{
    public string Password { get; set; } = string.Empty;
}

public class SaveEmployeeScheduleRequest
{
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
}

public class BulkEmployeeScheduleRequest : SaveEmployeeScheduleRequest
{
    public List<string> FinancialNumbers { get; set; } = new();
}

public class SaveEmployeeManagerRequest
{
    public string? ManagerFinancialNo { get; set; }
}
