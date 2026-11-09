using AttendanceApp.Data;
using AttendanceApp.Models;
using AttendanceApp.Services;
using Microsoft.AspNetCore.Identity;
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
    builder.Configuration.GetValue<int>("ZkSync:BatchSize", 1000),
    builder.Configuration.GetValue<int>("ZkSync:ReconcileIntervalSeconds", 30),
    builder.Configuration.GetValue<int>("ZkSync:ReconcileLookbackDays", 3));

builder.Services.AddSingleton(listenerOptions);
builder.Services.AddHostedService<MultiTargetZkSyncService>();

var app = builder.Build();

foreach (var target in listenerOptions.SqlTargets)
{
    var factory = DbContextFactoryBuilder.Create(target.ConnectionString);
    var dbService = new DatabaseService(
        factory,
        new PasswordHasher<AppUser>(),
        builder.Configuration);
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
    int BatchSize,
    int ReconcileIntervalSeconds,
    int ReconcileLookbackDays);

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
    private DateTime _lastReconcileUtc = DateTime.MinValue;

    public MultiTargetZkSyncService(ZkListenerOptions options, ILogger<MultiTargetZkSyncService> logger)
    {
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "ZK listener starting for {TargetCount} SQL target(s), polling every {Interval}s, " +
            "reconciling every {ReconcileInterval}s over the last {LookbackDays} day(s)",
            _options.SqlTargets.Count,
            _options.PollingIntervalSeconds,
            _options.ReconcileIntervalSeconds,
            _options.ReconcileLookbackDays);

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

            // New transactions are synced above by id. This periodic pass repairs
            // existing employee/day rows whose ZK transaction was edited in place,
            // deleted, or re-uploaded without a new id.
            if (_options.ReconcileIntervalSeconds > 0
                && (DateTime.UtcNow - _lastReconcileUtc).TotalSeconds >= _options.ReconcileIntervalSeconds)
            {
                foreach (var target in _options.SqlTargets)
                {
                    try
                    {
                        await ReconcileTargetAsync(target, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error reconciling ZK day updates to SQL target {TargetName}", target.Name);
                    }
                }

                _lastReconcileUtc = DateTime.UtcNow;
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

    private async Task ReconcileTargetAsync(SqlTargetOptions target, CancellationToken ct)
    {
        var factory = DbContextFactoryBuilder.Create(target.ConnectionString);
        var zkService = new ZkAttendanceService(_options.PostgreSqlConnectionString, factory);
        var today = DateTime.Now.Date;
        var fromDate = today.AddDays(-Math.Max(0, _options.ReconcileLookbackDays));

        var result = await zkService.ReconcileRangeAsync(fromDate, today);

        if (result.ChangedDays > 0)
        {
            _logger.LogInformation(
                "Reconciled ZK day updates for {TargetName}: {FromDate:yyyy-MM-dd}..{ToDate:yyyy-MM-dd}, " +
                "examined {ExaminedDays} day(s), updated {ChangedDays}, created {CreatedDays}, " +
                "cleared {ClearedDays}, affected {AffectedEmployees} employee(s)",
                target.Name,
                result.FromDate,
                result.ToDate,
                result.ExaminedDays,
                result.ChangedDays,
                result.CreatedDays,
                result.ClearedDays,
                result.AffectedEmployees);
        }
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
