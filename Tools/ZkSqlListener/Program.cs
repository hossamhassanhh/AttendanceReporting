using AttendanceApp.Data;
using AttendanceApp.Services;
using Microsoft.EntityFrameworkCore;
using System.Xml;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<HostOptions>(options =>
{
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
});

var connectionString = builder.Configuration.GetConnectionString("PostgreSql");
if (File.Exists("dynamic.config"))
{
    var doc = new XmlDocument();
    doc.Load(Path.GetFullPath("dynamic.config"));
    var connNode = doc.SelectSingleNode("//connectionStrings/add[@name='PostgreSql']");
    if (connNode?.Attributes?["connectionString"] != null)
        connectionString = connNode.Attributes["connectionString"]!.Value;
}

var sqlConnString = builder.Configuration.GetConnectionString("SqlServer")
    ?? "Server=.\\SQLEXPRESS;Database=AttendanceApp;Trusted_Connection=True;TrustServerCertificate=True;";

builder.Services.AddDbContextFactory<AppDbContext>(options => options.UseSqlServer(sqlConnString));
builder.Services.AddSingleton<DatabaseService>();
builder.Services.AddScoped<ZkAttendanceService>(sp =>
    new ZkAttendanceService(connectionString ?? string.Empty, sp.GetRequiredService<IDbContextFactory<AppDbContext>>()));
builder.Services.AddHostedService<ZkSyncBackgroundService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbService = scope.ServiceProvider.GetRequiredService<DatabaseService>();
    await dbService.InitializeAsync();
    await dbService.EnsureSyncStateTableAsync();
}

await app.RunAsync();
