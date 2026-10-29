using AttendanceApp.Data;
using AttendanceApp.Services;
using Microsoft.EntityFrameworkCore;

// Read-only dry run: prints how the new month-ordered status rules would
// reclassify stored statuses for a given month. Never writes to the database.
var connection = args switch
{
    _ when args.Contains("--connection") => args[Array.IndexOf(args, "--connection") + 1],
    _ => "Server=10.51.0.100;Database=AttendanceApp;Trusted_Connection=True;Encrypt=False;TrustServerCertificate=True;"
};
var monthStart = args switch
{
    _ when args.Contains("--month") => DateTime.Parse(args[Array.IndexOf(args, "--month") + 1]).Date,
    _ => new DateTime(2026, 10, 1)
};
monthStart = new DateTime(monthStart.Year, monthStart.Month, 1);
var rangeEnd = monthStart.AddMonths(1).AddDays(-1);

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlServer(connection)
    .Options;
using var db = new AppDbContext(options);

var records = await db.DailyAttendances
    .AsNoTracking()
    .Where(d => d.Date >= monthStart && d.Date <= rangeEnd)
    .ToListAsync();
var calendarSettings = await db.AttendanceDaySettings
    .AsNoTracking()
    .Where(s => s.Date >= monthStart && s.Date <= rangeEnd)
    .ToListAsync();
var employeeNos = records
    .Select(r => r.EmployeeFinancialNo)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToList();
var statusContext = await AttendanceTrackerService.LoadMonthlyStatusContextAsync(
    db, employeeNos, monthStart, rangeEnd);

var outcomes = AttendanceStatusRules.CalculateMonthlyStatuses(
    records,
    statusContext.LeaveFlags,
    calendarSettings,
    statusContext.LeaveTypeCodes);

Console.WriteLine($"Month: {monthStart:yyyy-MM}");
Console.WriteLine($"Records: {records.Count}; evaluated by walk: {outcomes.Count}");

if (args.Contains("--allowance"))
{
    // Per-employee month-end snapshot, computed exactly like
    // AttendanceTrackerService.BuildMonthlyLateAllowanceAsync (walk outcomes).
    var perEmployee = records
        .Where(r => outcomes.ContainsKey(r.Id))
        .GroupBy(r => r.EmployeeFinancialNo)
        .Select(g =>
        {
            var ordered = g.OrderBy(r => r.Date).ThenBy(r => r.Id).ToList();
            var graceUsed = ordered.Sum(r => outcomes[r.Id].GraceConsumedMinutes);
            var hoursUsed = ordered.Sum(r => outcomes[r.Id].LateHoursConsumed);
            var lateMinutes = ordered.Sum(r => outcomes[r.Id].DailyLateMinutes);
            var last = ordered.Last();
            var schedule = string.IsNullOrWhiteSpace(last.ScheduledStart)
                ? "-"
                : $"{last.ScheduledStart}-{last.ScheduledEnd}";
            return new
            {
                Employee = g.Key,
                Schedule = schedule,
                LateDays = ordered.Count(r => outcomes[r.Id].DailyLateMinutes > 0),
                LateMinutes = lateMinutes,
                GraceUsed = graceUsed,
                GraceRemaining = AttendanceStatusRules.GraceLateAllowanceMinutes - graceUsed,
                HoursUsed = hoursUsed,
                HoursRemaining = AttendanceStatusRules.LatePermissionMonthlyHours - hoursUsed
            };
        })
        .OrderByDescending(e => e.GraceUsed)
        .ThenBy(e => e.Employee)
        .ToList();

    Console.WriteLine();
    Console.WriteLine($"Employees: {perEmployee.Count}; employees who used grace: {perEmployee.Count(e => e.GraceUsed > 0)}");
    Console.WriteLine("Employee | Schedule | LateDays | LateMin | GraceUsed | GraceRemaining | HoursUsed | HoursRemaining");
    foreach (var e in perEmployee.Take(30))
        Console.WriteLine($"{e.Employee} | {e.Schedule} | {e.LateDays} | {e.LateMinutes} | {e.GraceUsed} | {e.GraceRemaining} | {e.HoursUsed} | {e.HoursRemaining}");

    var graceHistogram = perEmployee
        .GroupBy(e => e.GraceRemaining)
        .OrderByDescending(g => g.Key)
        .Select(g => $"{g.Key} min: {g.Count()} employees");
    Console.WriteLine();
    Console.WriteLine("Grace remaining distribution: " + string.Join("; ", graceHistogram));
    return;
}

var transitions = new Dictionary<string, int>(StringComparer.Ordinal);
var samples = new List<string>();
foreach (var record in records)
{
    if (!outcomes.TryGetValue(record.Id, out var outcome))
        continue;
    if (string.Equals(outcome.Status, record.Status, StringComparison.Ordinal))
        continue;

    var key = $"{record.Status} -> {outcome.Status}";
    transitions[key] = transitions.GetValueOrDefault(key) + 1;

    if (samples.Count < 25)
    {
        samples.Add(
            $"  {record.EmployeeFinancialNo} {record.Date:yyyy-MM-dd}: {record.Status} -> {outcome.Status}" +
            $" (late {outcome.DailyLateMinutes}m, grace -{outcome.GraceConsumedMinutes}m, hours -{outcome.LateHoursConsumed})");
    }
}

if (transitions.Count == 0)
{
    Console.WriteLine("No status changes: stored statuses already match the new rules.");
    return;
}

Console.WriteLine("Transitions:");
foreach (var transition in transitions.OrderByDescending(t => t.Value))
    Console.WriteLine($"  {transition.Key}: {transition.Value}");

Console.WriteLine("Samples:");
foreach (var sample in samples)
    Console.WriteLine(sample);
