using AttendanceApp.Data;
using AttendanceApp.Models;
using AttendanceApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AttendanceApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImportController : ControllerBase
{
    private readonly ExcelImportService _importService;
    private readonly ZkAttendanceService _zkService;
    private readonly LeaveFillService _leaveFillService;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public ImportController(ExcelImportService importService, ZkAttendanceService zkService, LeaveFillService leaveFillService, IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _importService = importService;
        _zkService = zkService;
        _leaveFillService = leaveFillService;
        _dbContextFactory = dbContextFactory;
    }

    [HttpPost("all")]
    public async Task<IActionResult> ImportAll()
    {
        try
        {
            var result = await _importService.ImportAllAsync();
            return Ok(new { message = result });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("zk-attendance")]
    public async Task<IActionResult> PopulateZkAttendance([FromQuery] string? date, [FromQuery] string? from, [FromQuery] string? to)
    {
        try
        {
            if (!string.IsNullOrEmpty(from) && !string.IsNullOrEmpty(to)
                && DateTime.TryParse(from, out var fromDate) && DateTime.TryParse(to, out var toDate))
            {
                var result = await _zkService.PopulateDailyAttendanceRangeAsync(fromDate, toDate);
                return Ok(new { message = result });
            }

            var d = DateTime.TryParse(date, out var dt) ? dt : DateTime.Today;
            var single = await _zkService.PopulateDailyAttendanceRangeAsync(d, d);
            return Ok(new { message = single });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("fill-leaves")]
    public async Task<IActionResult> FillLeaves([FromQuery] int? year, [FromQuery] int? month)
    {
        try
        {
            var y = year ?? 2026;
            var m = month ?? 5;
            var result = await _leaveFillService.FillLeavesFromMonthlyAsync(y, m);
            return Ok(new { message = result });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("sync-status")]
    public IActionResult GetSyncStatus()
    {
        return Ok(new
        {
            lastSyncTime = ZkSyncBackgroundService.LastSyncTime,
            lastProcessedId = ZkSyncBackgroundService.LastProcessedId,
            isRunning = ZkSyncBackgroundService.IsRunning
        });
    }

    [HttpPost("zk-backfill")]
    public async Task<IActionResult> BackfillAll()
    {
        try
        {
            var result = await _zkService.BackfillAllAsync();

            using var db = await _dbContextFactory.CreateDbContextAsync();
            var state = await db.SyncStates.FirstOrDefaultAsync(s => s.Key == "ZkAttendance");
            if (state == null)
            {
                state = new SyncState { Key = "ZkAttendance" };
                db.SyncStates.Add(state);
            }
            state.LastProcessedTransactionId = result.MaxId;
            state.LastSyncTime = DateTime.Now;
            await db.SaveChangesAsync();

            return Ok(new { message = $"Backfill complete: {result.ProcessedCount} records processed, lastId={result.MaxId}" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
