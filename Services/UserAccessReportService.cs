using AttendanceApp.Data;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace AttendanceApp.Services;

public sealed class UserAccessReportService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<UserAccessReportService> _logger;

    public UserAccessReportService(IServiceScopeFactory scopeFactory, ILogger<UserAccessReportService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            using var db = await factory.CreateDbContextAsync(cancellationToken);
            var users = await db.AppUsers.AsNoTracking()
                .OrderBy(user => user.Username)
                .Select(user => new { user.Username, user.DisplayName, user.Role, user.IsActive, user.MustChangePassword })
                .ToListAsync(cancellationToken);

            var output = new StringBuilder();
            output.AppendLine("Attendance Reporting - User Access Report");
            output.AppendLine($"Generated (UTC): {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
            output.AppendLine();
            output.AppendLine("Passwords are deliberately not included. They are stored as one-way hashes and cannot be recovered.");
            output.AppendLine("Use the Administrator password-reset feature to issue a new temporary password when required.");
            output.AppendLine();

            foreach (var user in users)
            {
                output.AppendLine($"Username: {user.Username}");
                output.AppendLine($"Display name: {user.DisplayName}");
                output.AppendLine($"Role: {user.Role}");
                output.AppendLine($"Active: {(user.IsActive ? "Yes" : "No")}");
                output.AppendLine($"Password: Protected - reset required to issue a replacement");
                output.AppendLine($"Must change password: {(user.MustChangePassword ? "Yes" : "No")}");
                output.AppendLine();
            }

            var reportDirectory = Path.Combine(AppContext.BaseDirectory, "App_Data", "Reports");
            Directory.CreateDirectory(reportDirectory);
            await File.WriteAllTextAsync(Path.Combine(reportDirectory, "user-access-report.txt"), output.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to generate the user access report");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
