using AttendanceApp.Data;
using AttendanceApp.Services;
using Microsoft.EntityFrameworkCore;
using System.Xml;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<HostOptions>(options =>
{
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
});

builder.Services.AddControllers();

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

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlServer(sqlConnString));

builder.Services.AddSingleton<DatabaseService>();
builder.Services.AddScoped<ExcelImportService>();
builder.Services.AddScoped<AttendanceTrackerService>();
builder.Services.AddScoped<LeaveService>();
builder.Services.AddScoped<ZkAttendanceService>(sp =>
    new ZkAttendanceService(connectionString ?? string.Empty, sp.GetRequiredService<IDbContextFactory<AppDbContext>>()));
builder.Services.AddScoped<LeaveFillService>();
builder.Services.AddScoped<ExportService>();
builder.Services.AddHostedService<ZkSyncBackgroundService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbService = scope.ServiceProvider.GetRequiredService<DatabaseService>();
    await dbService.InitializeAsync();
    await dbService.EnsureSyncStateTableAsync();
}

app.UseCors();
app.UseStaticFiles();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
