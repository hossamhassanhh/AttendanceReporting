using AttendanceApp.Data;
using AttendanceApp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

// Runs the production recalculation (AttendanceTrackerService.RecalculateStatusesAsync)
// against the target database using the current code, with a backup table first.
// Default is dry-run; pass --apply to write.
var connection = args.Contains("--connection")
    ? args[Array.IndexOf(args, "--connection") + 1]
    : "Server=10.51.0.100;Database=AttendanceApp;Trusted_Connection=True;Encrypt=False;TrustServerCertificate=True;";
var fromDate = args.Contains("--from")
    ? DateTime.Parse(args[Array.IndexOf(args, "--from") + 1]).Date
    : new DateTime(2026, 9, 1);
var toDate = args.Contains("--to")
    ? DateTime.Parse(args[Array.IndexOf(args, "--to") + 1]).Date
    : new DateTime(2026, 10, 31);
var apply = args.Contains("--apply");

Console.WriteLine($"Recalculation range: {fromDate:yyyy-MM-dd} .. {toDate:yyyy-MM-dd}");
Console.WriteLine(apply ? "Mode: APPLY (writes statuses, backup table first)" : "Mode: dry-run (no writes; use --apply)");

if (!apply)
{
    Console.WriteLine("Dry-run preview available via: dotnet run --project Tools/StatusRulesDryRun -- --month <yyyy-MM>");
    return;
}

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlServer(connection)
    .Options;
var factory = new PooledDbContextFactory<AppDbContext>(options);

// Backup every row in the range so the recalculation can be reverted.
var backupTable = $"DailyAttendances_StatusBackup_{DateTime.Now:yyyyMMddHHmmss}";
await using (var db = await factory.CreateDbContextAsync())
{
    // Table names cannot be parameterized: build the identifier explicitly.
    await db.Database.ExecuteSqlRawAsync(
        $"SELECT * INTO [{backupTable}] FROM [DailyAttendances] WHERE [Date] >= {{0}} AND [Date] <= {{1}}",
        fromDate,
        toDate);
    var backupCount = await db.Database
        .SqlQueryRaw<int>($"SELECT COUNT(*) AS Value FROM [{backupTable}]")
        .SingleAsync();
    Console.WriteLine($"Backup written: [{backupTable}] ({backupCount} rows)");
}

var tracker = new AttendanceTrackerService(factory);
var result = await tracker.RecalculateStatusesAsync(
    fromDate, toDate, null, null, null, null, null, null);

Console.WriteLine(
    $"Recalculated: processed {result.ProcessedCount}, updated {result.UpdatedCount}, " +
    $"unchanged {result.UnchangedCount}, skipped {result.SkippedCount}");
Console.WriteLine($"Revert with: SELECT * FROM [{backupTable}]  (restore by Id into DailyAttendances)");
