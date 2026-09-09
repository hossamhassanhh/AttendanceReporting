using AttendanceApp.Data;
using AttendanceApp.Models;
using AttendanceApp.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;
using System.Security.Claims;
using System.Xml;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<HostOptions>(options =>
{
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
});

builder.Services.AddControllers();
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
    options.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
    options.Level = CompressionLevel.Fastest);
builder.Services.AddSingleton<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
builder.Services
    .AddAuthentication(AuthConstants.Scheme)
    .AddCookie(AuthConstants.Scheme, options =>
    {
        options.Cookie.Name = "AttendanceReporting.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.SlidingExpiration = true;
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            },
            OnValidatePrincipal = async context =>
            {
                var userIdValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                var versionValue = context.Principal?.FindFirstValue("session_version");
                if (!int.TryParse(userIdValue, out var userId)
                    || !int.TryParse(versionValue, out var sessionVersion))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(AuthConstants.Scheme);
                    return;
                }

                var factory = context.HttpContext.RequestServices
                    .GetRequiredService<IDbContextFactory<AppDbContext>>();
                using var db = await factory.CreateDbContextAsync();
                var user = await db.AppUsers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == userId);
                if (user == null || !user.IsActive || user.SessionVersion != sessionVersion)
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(AuthConstants.Scheme);
                }
            }
        };
    });
builder.Services.AddAuthorization(options =>
{
    foreach (var permission in PermissionCatalog.All)
    {
        options.AddPolicy(permission, policy =>
            policy.RequireClaim(AuthConstants.PermissionClaim, permission));
    }

    options.AddPolicy("LeaveFlow", policy =>
        policy.RequireAssertion(context =>
            context.User.HasClaim(AuthConstants.PermissionClaim, "Leaves")
            || context.User.HasClaim(AuthConstants.PermissionClaim, "LeaveHR")
            || context.User.HasClaim(AuthConstants.PermissionClaim, "LeaveApproval")
            || context.User.HasClaim(AuthConstants.PermissionClaim, "SelfLeave")));

    options.AddPolicy("LeaveHrFlow", policy =>
        policy.RequireAssertion(context =>
            context.User.HasClaim(AuthConstants.PermissionClaim, "Leaves")
            || context.User.HasClaim(AuthConstants.PermissionClaim, "LeaveHR")));

    options.AddPolicy("LeaveApprovalFlow", policy =>
        policy.RequireAssertion(context =>
            context.User.HasClaim(AuthConstants.PermissionClaim, "Leaves")
            || context.User.HasClaim(AuthConstants.PermissionClaim, "LeaveHR")
            || context.User.HasClaim(AuthConstants.PermissionClaim, "LeaveApproval")));

    options.AddPolicy("SelfOrAdmin", policy =>
        policy.RequireAssertion(context =>
            context.User.HasClaim(AuthConstants.PermissionClaim, "Attendance")
            || context.User.HasClaim(AuthConstants.PermissionClaim, "SelfAttendance")));

    options.AddPolicy("SystemOwner", policy =>
        policy.RequireClaim(ClaimTypes.Role, "SystemOwner"));
});

var connectionString = builder.Configuration.GetConnectionString("PostgreSql");

if (File.Exists("dynamic.config"))
{
    var configPath = Path.GetFullPath("dynamic.config");
    var doc = new XmlDocument();
    doc.Load(configPath);
    var connNode = doc.SelectSingleNode("//connectionStrings/add[@name='PostgreSql']");
    if (connNode?.Attributes?["connectionString"] != null)
    {
        connectionString = connNode.Attributes!["connectionString"]!.Value;
    }
}

builder.Services.AddSingleton(new AttendanceService(connectionString ?? string.Empty));

var sqlConnString = builder.Configuration.GetConnectionString("SqlServer")
    ?? "Server=.\\SQLEXPRESS;Database=AttendanceApp;Trusted_Connection=True;TrustServerCertificate=True;";
var useSqliteForTesting = builder.Configuration.GetValue<bool>("Testing:UseSqlite");

if (useSqliteForTesting)
{
    var sqlitePath = builder.Configuration["Testing:SqlitePath"]
        ?? Path.Combine(Path.GetTempPath(), "attendance-reporting-browser-test.db");
    builder.Services.AddPooledDbContextFactory<AppDbContext>(
        options => options.UseSqlite($"Data Source={sqlitePath}"),
        poolSize: 32);
}
else
{
    builder.Services.AddPooledDbContextFactory<AppDbContext>(
        options => options.UseSqlServer(sqlConnString),
        poolSize: 128);
}

builder.Services.AddSingleton<DatabaseService>();
builder.Services.AddSingleton<AdDirectoryService>();
builder.Services.AddHostedService<AdSyncBackgroundService>();
builder.Services.AddScoped<ExcelImportService>();
builder.Services.AddScoped<AttendanceTrackerService>();
builder.Services.AddScoped<LeaveService>();
builder.Services.AddScoped<ZkAttendanceService>(sp =>
    new ZkAttendanceService(connectionString ?? string.Empty, sp.GetRequiredService<IDbContextFactory<AppDbContext>>()));
builder.Services.AddScoped<LeaveFillService>();
builder.Services.AddScoped<ExportService>();
builder.Services.AddScoped<TabularReportExportService>();
builder.Services.AddHostedService<WorkScheduleWorkbookImportService>();
builder.Services.AddHostedService<PendingLeaveHistoryImportService>();
builder.Services.AddHostedService<UserAccessReportService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    if (useSqliteForTesting)
    {
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<AppUser>>();
        using var db = await factory.CreateDbContextAsync();
        await db.Database.EnsureCreatedAsync();

        if (!await db.AppUsers.AnyAsync())
        {
            var admin = new AppUser
            {
                Username = "admin",
                DisplayName = "Administrator",
                DisplayNameAr = "مدير النظام",
                DisplayNameEn = "Administrator",
                Role = "Admin",
                Permissions = PermissionCatalog.AdminPermissions,
                IsActive = true,
                MustChangePassword = false
            };
            admin.PasswordHash = passwordHasher.HashPassword(admin, "Admin@2026!");
            db.AppUsers.Add(admin);
        }

        if (!await db.LeaveTypes.AnyAsync())
        {
            db.LeaveTypes.Add(new LeaveType
            {
                Code = "A",
                NameAr = "إجازة اعتيادية",
                NameEn = "Regular Leave",
                BalanceType = "Regular"
            });
        }

        await db.SaveChangesAsync();
    }
    else
    {
        var dbService = scope.ServiceProvider.GetRequiredService<DatabaseService>();
        await dbService.InitializeAsync();
        await dbService.EnsureSyncStateTableAsync();
    }
}

app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers.Expires = "0";
        return Task.CompletedTask;
    });

    await next();
});
app.UseResponseCompression();
app.UseStaticFiles();
app.UseAuthentication();
app.Use(async (context, next) =>
{
    var mustChangePassword = context.User.FindFirst("must_change_password")?.Value == "true";
    if (mustChangePassword
        && context.Request.Path.StartsWithSegments("/api")
        && !context.Request.Path.StartsWithSegments("/api/auth"))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new { error = "Password change required" });
        return;
    }

    await next();
});
app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
