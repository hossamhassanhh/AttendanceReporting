using AttendanceApp.Data;
using AttendanceApp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

// Usage:
//   dotnet run --project Tools/ZkReconcileTest -- [--days 3] [--pg <conn>] [--sql <conn>]
// Reconciles the ZK transactions of the last N days into DailyAttendances and
// reports the result. Run twice to confirm idempotency.
var pgConnection = args.SkipWhile(a => a != "--pg").Skip(1).FirstOrDefault()
    ?? "Host=10.51.0.85;Port=7496;Database=biotime;Username=postgres;Password=H0$$@m@123";
var sqlConnection = args.SkipWhile(a => a != "--sql").Skip(1).FirstOrDefault()
    ?? "Server=10.51.0.100;Database=AttendanceApp;Trusted_Connection=True;Encrypt=False;TrustServerCertificate=True;";
var days = args.SkipWhile(a => a != "--days").Skip(1).Select(int.Parse).FirstOrDefault();
if (days <= 0) days = 3;

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlServer(sqlConnection)
    .Options;
var factory = new PooledDbContextFactory<AppDbContext>(options);
var service = new ZkAttendanceService(pgConnection, factory);

var today = DateTime.Now.Date;
var fromDate = today.AddDays(-days);

for (var run = 1; run <= 2; run++)
{
    var result = await service.ReconcileRangeAsync(fromDate, today);
    Console.WriteLine(
        $"Run {run}: {result.FromDate:yyyy-MM-dd}..{result.ToDate:yyyy-MM-dd} | " +
        $"ZK employee-days={result.ExaminedDays}, changed={result.ChangedDays}, " +
        $"created={result.CreatedDays}, cleared={result.ClearedDays}, " +
        $"affected employees={result.AffectedEmployees}");
}
