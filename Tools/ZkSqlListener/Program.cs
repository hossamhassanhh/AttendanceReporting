using AttendanceApp.Data;
using AttendanceApp.Services;
using Microsoft.EntityFrameworkCore;
using System.Xml;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<HostOptions>(options =>
{
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
});

var pgConnectionString = builder.Configuration.GetConnectionString("PostgreSql");
if (File.Exists("dynamic.config"))
{
    var doc = new XmlDocument();
    doc.Load(Path.GetFullPath("dynamic.config"));
    var connNode = doc.SelectSingleNode("//connectionStrings/add[@name='PostgreSql']");
    if (connNode?.Attributes?["connectionString"] != null)
        pgConnectionString = connNode.Attributes["connectionString"]!.Value;
}

var sqlTargets = builder.Configuration.GetSection("SqlTargets").Get<List<SqlTargetOptions>>()
    ?.Where(t => !string.IsNullOrWhiteSpace(t.ConnectionString))
    .ToList() ?? new List<SqlTargetOptions>();

if (sqlTargets.Count == 0)
{
    sqlTargets.Add(new SqlTargetOptions
    {
        Name = "Default",
        ConnectionString = builder.Configuration.GetConnectionString("SqlServer")
            ?? "Server=.\\SQLEXPRESS;Database=AttendanceApp;Trusted_Connection=True;TrustServerCertificate=True;"
    });
}

var listenerOptions = new ZkListenerOptions(
    pgConnectionString ?? string.Empty,
    sqlTargets,
    builder.Configuration.GetValue<int>("ZkSync:PollingIntervalSeconds", 60),
    builder.Configuration.GetValue<int>("ZkSync:BatchSize", 1000));

builder.Services.AddSingleton(listenerOptions);
builder.Services.AddHostedService<MultiTargetZkSyncService>();

var app = builder.Build();

foreach (var target in listenerOptions.SqlTargets)
{
    var factory = DbContextFactoryBuilder.Create(target.ConnectionString);
    var dbService = new DatabaseService(factory);
    await dbService.InitializeAsync();
    await dbService.EnsureSyncStateTableAsync();
}

await app.RunAsync();

public sealed class SqlTargetOptions
{
    public string Name { get; set; } = "SQL";
    public string ConnectionString { get; set; } = string.Empty;
}

public sealed record ZkListenerOptions(
    string PostgreSqlConnectionString,
    IReadOnlyList<SqlTargetOptions> SqlTargets,
    int PollingIntervalSeconds,
    int BatchSize);

public sealed class StaticDbContextFactory : IDbContextFactory<AppDbContext>
{
    private readonly DbContextOptions<AppDbContext> _options;

    public StaticDbContextFactory(DbContextOptions<AppDbContext> options)
    {
        _options = options;
    }

    public AppDbContext CreateDbContext()
    {
        return new AppDbContext(_options);
    }
}

public static class DbContextFactoryBuilder
{
    public static StaticDbContextFactory Create(string connectionString)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new StaticDbContextFactory(options);
    }
}

public sealed class MultiTargetZkSyncService : BackgroundService
{
    private readonly ZkListenerOptions _options;
    private readonly ILogger<MultiTargetZkSyncService> _logger;

    public MultiTargetZkSyncService(ZkListenerOptions options, ILogger<MultiTargetZkSyncService> logger)
    {
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "ZK listener starting for {TargetCount} SQL target(s), polling every {Interval}s",
            _options.SqlTargets.Count,
            _options.PollingIntervalSeconds);

        foreach (var target in _options.SqlTargets)
            _logger.LogInformation("SQL target enabled: {TargetName}", target.Name);

        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var target in _options.SqlTargets)
            {
                try
                {
                    await SyncTargetAsync(target, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error syncing ZK attendance to SQL target {TargetName}", target.Name);
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.PollingIntervalSeconds), stoppingToken);
        }
    }

    private async Task SyncTargetAsync(SqlTargetOptions target, CancellationToken ct)
    {
        var factory = DbContextFactoryBuilder.Create(target.ConnectionString);
        var lastProcessedId = await GetLastProcessedIdAsync(factory, ct);
        var zkService = new ZkAttendanceService(_options.PostgreSqlConnectionString, factory);
        var result = await zkService.SyncNewPunchesAsync(lastProcessedId, _options.BatchSize);

        if (result.MaxId <= lastProcessedId)
        {
            await UpdateSyncStateAsync(factory, lastProcessedId, ct);
            return;
        }

        await UpdateSyncStateAsync(factory, result.MaxId, ct);
        _logger.LogInformation(
            "Synced {Count} attendance record(s) to {TargetName}, lastId={MaxId}",
            result.ProcessedCount,
            target.Name,
            result.MaxId);
    }

    private static async Task<long> GetLastProcessedIdAsync(IDbContextFactory<AppDbContext> factory, CancellationToken ct)
    {
        using var context = await factory.CreateDbContextAsync(ct);
        var state = await context.SyncStates.FirstOrDefaultAsync(s => s.Key == "ZkAttendance", ct);
        return state?.LastProcessedTransactionId ?? 0;
    }

    private static async Task UpdateSyncStateAsync(IDbContextFactory<AppDbContext> factory, long maxId, CancellationToken ct)
    {
        using var context = await factory.CreateDbContextAsync(ct);
        var state = await context.SyncStates.FirstOrDefaultAsync(s => s.Key == "ZkAttendance", ct);

        if (state == null)
        {
            context.SyncStates.Add(new AttendanceApp.Models.SyncState
            {
                Key = "ZkAttendance",
                LastProcessedTransactionId = maxId,
                LastSyncTime = DateTime.Now
            });
        }
        else
        {
            state.LastProcessedTransactionId = maxId;
            state.LastSyncTime = DateTime.Now;
        }

        await context.SaveChangesAsync(ct);
    }
}
