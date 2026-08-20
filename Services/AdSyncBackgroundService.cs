using AttendanceApp.Services;

namespace AttendanceApp.Services;

public class AdSyncBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdSyncBackgroundService> _logger;

    public AdSyncBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<AdSyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        var lastRun = DateTime.MinValue;
        var interval = TimeSpan.FromMinutes(
            _configuration.GetValue("ActiveDirectory:SyncIntervalMinutes", 60));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (DateTime.Now - lastRun >= interval)
                {
                    await SyncOnceAsync(stoppingToken);
                    lastRun = DateTime.Now;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "AD background sync failed");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task SyncOnceAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<AdDirectoryService>();
        if (!service.Enabled)
            return;

        var result = await service.SyncUsersAsync();
        _logger.LogInformation(
            "AD sync ran: {Added} added, {Updated} updated, {Skipped} unchanged",
            result.Added, result.Updated, result.Skipped);
        stoppingToken.ThrowIfCancellationRequested();
    }
}