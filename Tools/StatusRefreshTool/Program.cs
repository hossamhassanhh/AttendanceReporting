using AttendanceApp.Data;
using AttendanceApp.Services;
using Microsoft.EntityFrameworkCore;

var connection = args switch
{
    _ when args.Contains("--connection") => args[Array.IndexOf(args, "--connection") + 1],
    _ => "Server=10.51.0.100;Database=AttendanceApp;Trusted_Connection=True;Encrypt=False;TrustServerCertificate=True;"
};
var dryRun = args.Contains("--dry-run");
var backupTable = $"DailyAttendances_StatusBackup_{DateTime.Now:yyyyMMdd}";

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlServer(connection)
    .Options;
using var db = new AppDbContext(options);

var calendarSettings = await db.AttendanceDaySettings.AsNoTracking().ToListAsync();

// Only stored "Late" rows can be affected by the early-leave-precedence change.
var rows = await db.DailyAttendances
    .AsNoTracking()
    .Where(a => a.LeaveTypeId == null && a.Status == "Late" && a.FirstPunch != null)
    .Select(a => new { a.Id, a.FirstPunch, a.LastPunch, a.ScheduledStart, a.ScheduledEnd, a.Date, a.Status })
    .ToListAsync();

var transitions = new Dictionary<int, string>();
foreach (var row in rows)
{
    var newStatus = AttendanceStatusRules.Calculate(
        row.FirstPunch, row.LastPunch, row.ScheduledStart, row.ScheduledEnd,
        row.Date, calendarSettings);
    if (!string.Equals(newStatus, row.Status, StringComparison.Ordinal))
        transitions[row.Id] = newStatus;
}

var toEarlyLeave = transitions.Where(t => t.Value == "Early Leave").Select(t => t.Key).ToList();
var other = transitions.Where(t => t.Value != "Early Leave").ToList();

Console.WriteLine($"Stored 'Late' candidates: {rows.Count}");
Console.WriteLine($"  -> Early Leave (the reorder effect): {toEarlyLeave.Count}");
foreach (var group in other.GroupBy(t => t.Value))
    Console.WriteLine($"  -> {group.Key}: {group.Count()} (NOT applied)");

if (dryRun)
{
    Console.WriteLine("DRY RUN - no writes performed.");
    return;
}

if (toEarlyLeave.Count == 0)
{
    Console.WriteLine("No rows to update.");
    return;
}

var ids = string.Join(',', toEarlyLeave);
await db.Database.ExecuteSqlRawAsync(
    $"IF OBJECT_ID('{backupTable}', 'U') IS NULL SELECT * INTO [{backupTable}] FROM [DailyAttendances] WHERE [Id] IN ({ids});");
Console.WriteLine($"Backup written: [{backupTable}] ({toEarlyLeave.Count} rows)");

foreach (var chunk in toEarlyLeave.Chunk(500))
{
    var entities = await db.DailyAttendances.Where(a => chunk.Contains(a.Id)).ToListAsync();
    foreach (var entity in entities)
        entity.Status = "Early Leave";
    await db.SaveChangesAsync();
}

Console.WriteLine("Stored statuses refreshed (Late -> Early Leave only).");
