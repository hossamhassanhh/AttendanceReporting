using AttendanceApp.Data;
using AttendanceApp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Globalization;

// Usage:
//   dotnet run --project Tools/ZkReconcileTest -- [--days 3] [--from yyyy-MM-dd] [--to yyyy-MM-dd]
//       [--employee 3868,4779] [--pg <conn>] [--sql <conn>]
// Reconciles the ZK transactions of the selected range into DailyAttendances and
// reports the result. Run twice to confirm idempotency.
var pgConnection = args.SkipWhile(a => a != "--pg").Skip(1).FirstOrDefault()
    ?? "Host=10.51.0.85;Port=7496;Database=biotime;Username=postgres;Password=H0$$@m@123";
var sqlConnection = args.SkipWhile(a => a != "--sql").Skip(1).FirstOrDefault()
    ?? "Server=10.51.0.100;Database=AttendanceApp;Trusted_Connection=True;Encrypt=False;TrustServerCertificate=True;";

var days = args.SkipWhile(a => a != "--days").Skip(1).Select(int.Parse).FirstOrDefault();
if (days <= 0) days = 3;

var today = DateTime.Now.Date;
var toDate = TryParseDate(args.SkipWhile(a => a != "--to").Skip(1).FirstOrDefault()) ?? today;
var fromDate = TryParseDate(args.SkipWhile(a => a != "--from").Skip(1).FirstOrDefault())
    ?? toDate.AddDays(-days);

var employeeFilter = args.SkipWhile(a => a != "--employee").Skip(1).FirstOrDefault()
    ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlServer(sqlConnection)
    .Options;
var factory = new PooledDbContextFactory<AppDbContext>(options);
var service = new ZkAttendanceService(pgConnection, factory);

Console.WriteLine(
    $"Scope: employees={(employeeFilter is { Length: > 0 } ? string.Join(",", employeeFilter) : "all active")}, " +
    $"{fromDate:yyyy-MM-dd}..{toDate:yyyy-MM-dd}");

for (var run = 1; run <= 2; run++)
{
    var result = await service.ReconcileRangeAsync(fromDate, toDate, employeeFilter);
    Console.WriteLine(
        $"Run {run}: {result.FromDate:yyyy-MM-dd}..{result.ToDate:yyyy-MM-dd} | " +
        $"ZK employee-days={result.ExaminedDays}, changed={result.ChangedDays}, " +
        $"created={result.CreatedDays}, cleared={result.ClearedDays}, " +
        $"affected employees={result.AffectedEmployees}");
}

static DateTime? TryParseDate(string? value) =>
    DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
        ? date.Date
        : null;
