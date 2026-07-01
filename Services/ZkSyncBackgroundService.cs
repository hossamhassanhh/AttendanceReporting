using AttendanceApp.Data;
using AttendanceApp.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceApp.Services;

public class ZkSyncBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ZkSyncBackgroundService> _logger;
    private readonly int _pollingIntervalSeconds;
    private readonly int _batchSize;

    public static DateTime? LastSyncTime { get; private set; }
    public static long LastProcessedId { get; private set; }
    public static bool IsRunning { get; private set; }

    public ZkSyncBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ZkSyncBackgroundService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _pollingIntervalSeconds = configuration.GetValue<int>("ZkSync:PollingIntervalSeconds", 60);
        _batchSize = configuration.GetValue<int>("ZkSync:BatchSize", 1000);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ZK Sync Background Service starting (polling every {Interval}s)", _pollingIntervalSeconds);

        await using var scope = _serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        LastProcessedId = await GetLastProcessedIdAsync(db);
        _logger.LogInformation("Starting from transaction ID {LastId}", LastProcessedId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                IsRunning = true;
                await SyncOnceAsync(db, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during ZK sync cycle");
            }
            finally
            {
                IsRunning = false;
            }

            await Task.Delay(TimeSpan.FromSeconds(_pollingIntervalSeconds), stoppingToken);
        }

        _logger.LogInformation("ZK Sync Background Service stopping");
    }

    private async Task SyncOnceAsync(IDbContextFactory<AppDbContext> db, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateAsyncScope();
        var zkService = scope.ServiceProvider.GetRequiredService<ZkAttendanceService>();

        var result = await zkService.SyncNewPunchesAsync(LastProcessedId, _batchSize);

        if (result.MaxId > LastProcessedId)
        {
            LastProcessedId = result.MaxId;
            LastSyncTime = DateTime.Now;

            await UpdateSyncStateAsync(db, result.MaxId);

            _logger.LogInformation("Synced {Count} attendance records, lastId={MaxId}",
                result.ProcessedCount, result.MaxId);
        }
    }

    private static async Task<long> GetLastProcessedIdAsync(IDbContextFactory<AppDbContext> db)
    {
        using var context = await db.CreateDbContextAsync();
        var state = await context.SyncStates.FirstOrDefaultAsync(s => s.Key == "ZkAttendance");
        return state?.LastProcessedTransactionId ?? 0;
    }

    private static async Task UpdateSyncStateAsync(IDbContextFactory<AppDbContext> db, long maxId)
    {
        using var context = await db.CreateDbContextAsync();
        var state = await context.SyncStates.FirstOrDefaultAsync(s => s.Key == "ZkAttendance");

        if (state == null)
        {
            state = new SyncState
            {
                Key = "ZkAttendance",
                LastProcessedTransactionId = maxId,
                LastSyncTime = DateTime.Now
            };
            context.SyncStates.Add(state);
        }
        else
        {
            state.LastProcessedTransactionId = maxId;
            state.LastSyncTime = DateTime.Now;
        }

        await context.SaveChangesAsync();
    }
}
