using AttendanceApp.Data;
using AttendanceApp.Models;
using AttendanceApp.Services;
using Microsoft.EntityFrameworkCore;

var connection = args switch
{
    _ when args.Contains("--connection") => args[Array.IndexOf(args, "--connection") + 1],
    _ => "Server=10.51.0.100;Database=AttendanceApp;Trusted_Connection=True;Encrypt=False;TrustServerCertificate=True;"
};
var dryRun = args.Contains("--dry-run");
var fromDate = new DateTime(2026, 10, 1);

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlServer(connection)
    .Options;
using var db = new AppDbContext(options);

var calendarSettings = await db.AttendanceDaySettings.AsNoTracking().ToListAsync();
var scheduleRules = await db.ScheduleRules.AsNoTracking().Where(r => r.IsActive).ToListAsync();

// Stale rows: stored as WFH by the old Sunday rule, now a working day.
// Leave rows (LeaveTypeId set) are never touched.
var rows = await db.DailyAttendances
    .Include(a => a.Employee)
    .Where(a => a.LeaveTypeId == null
        && a.Status == AttendanceCalendarRules.WorkFromHome
        && a.Date >= fromDate)
    .ToListAsync();

Console.WriteLine($"Stale Sunday WFH rows: {rows.Count}");

var transitions = new Dictionary<int, (string NewStatus, string? SchedStart, string? SchedEnd)>();
var counts = new Dictionary<string, int>(StringComparer.Ordinal);

foreach (var row in rows)
{
    var schedule = row.Employee == null
        ? null
        : AttendanceStatusRules.ResolveSchedule(row.Employee, scheduleRules);
    var start = schedule?.StartTime ?? row.ScheduledStart;
    var end = schedule?.EndTime ?? row.ScheduledEnd;
    var newStatus = AttendanceStatusRules.Calculate(
        row.FirstPunch, row.LastPunch, start, end, row.Date, calendarSettings);

    counts[newStatus] = counts.GetValueOrDefault(newStatus) + 1;

    if (!string.Equals(newStatus, row.Status, StringComparison.Ordinal)
        || !string.Equals(row.ScheduledStart, start, StringComparison.Ordinal)
        || !string.Equals(row.ScheduledEnd, end, StringComparison.Ordinal))
    {
        transitions[row.Id] = (newStatus, start, end);
    }
}

foreach (var group in counts.OrderBy(g => g.Key))
    Console.WriteLine($"  -> {group.Key}: {group.Value}");

Console.WriteLine($"Rows to update: {transitions.Count}");

if (dryRun)
{
    Console.WriteLine("DRY RUN - no writes performed.");
    return;
}

if (transitions.Count == 0)
{
    Console.WriteLine("No rows to update.");
    return;
}

var backupTable = $"DailyAttendances_WfhBackup_{DateTime.Now:yyyyMMddHHmmss}";
var ids = string.Join(',', transitions.Keys);
await db.Database.ExecuteSqlRawAsync(
    $"IF OBJECT_ID('{backupTable}', 'U') IS NULL SELECT * INTO [{backupTable}] FROM [DailyAttendances] WHERE [Id] IN ({ids});");
Console.WriteLine($"Backup written: [{backupTable}] ({transitions.Count} rows)");

var updated = 0;
foreach (var chunk in transitions.Chunk(500))
{
    var chunkIds = chunk.Select(c => c.Key).ToList();
    var entities = await db.DailyAttendances.Where(a => chunkIds.Contains(a.Id)).ToListAsync();
    foreach (var entity in entities)
    {
        var t = transitions[entity.Id];
        entity.Status = t.NewStatus;
        entity.ScheduledStart = t.SchedStart;
        entity.ScheduledEnd = t.SchedEnd;
        updated++;
    }
    await db.SaveChangesAsync();
    db.ChangeTracker.Clear();
}

Console.WriteLine($"Updated {updated} rows.");
