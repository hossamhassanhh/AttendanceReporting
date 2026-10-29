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

if (args.Contains("--summary"))
{
    // Exercises the same service method the /api/tracking/allowance-summary
    // endpoint uses, so the numbers are the ones the UI will display.
    var tracker = new AttendanceTrackerService(new SimpleDbContextFactory(options));
    var queried = await tracker.QueryAttendanceAsync(null, monthStart, rangeEnd);
    var summary = await tracker.BuildMonthlyAllowanceSummaryAsync(queried);

    Console.WriteLine($"Month: {monthStart:yyyy-MM}");
    Console.WriteLine($"Summary rows: {summary.Count}; employees: {summary.Select(s => s.FinancialNo).Distinct().Count()}");
    Console.WriteLine($"Employees who used grace: {summary.Count(s => s.GraceUsedMinutes > 0)}");
    Console.WriteLine($"Employees who used the 2h allowance: {summary.Count(s => s.LateHoursUsed > 0)}");
    Console.WriteLine($"Total late days: {summary.Sum(s => s.LateDays)}; total late minutes: {summary.Sum(s => s.LateMinutes)}");
    Console.WriteLine($"Total grace used: {summary.Sum(s => s.GraceUsedMinutes)} min; total hours used: {summary.Sum(s => s.LateHoursUsed)}");
    Console.WriteLine();

    var invalid = summary
        .Where(s => s.GraceUsedMinutes > AttendanceStatusRules.GraceLateAllowanceMinutes
            || s.LateHoursUsed > AttendanceStatusRules.LatePermissionMonthlyHours
            || s.GraceRemainingMinutes < 0
            || s.LateHoursRemaining < 0
            || s.LateDays < 0)
        .ToList();
    Console.WriteLine(invalid.Count == 0
        ? "Quota invariants: OK (no negative or over-budget rows)"
        : $"Quota invariants: FAILED for {invalid.Count} rows");
    foreach (var row in invalid.Take(10))
        Console.WriteLine($"  {row.FinancialNo} {row.MonthDisplay}: grace {row.GraceUsedMinutes}/{row.GraceRemainingMinutes}, hours {row.LateHoursUsed}/{row.LateHoursRemaining}");
    Console.WriteLine();

    Console.WriteLine("Fin | Month | Schedule | LateDays | LateMin | GraceUsed | GraceLeft | HoursUsed | HoursLeft | Name");
    foreach (var row in summary.Where(s => s.GraceUsedMinutes > 0).Take(15))
        Console.WriteLine($"{row.FinancialNo} | {row.MonthDisplay} | {row.Schedule} | {row.LateDays} | {row.LateMinutes} | {row.GraceUsedMinutes} | {row.GraceRemainingMinutes} | {row.LateHoursUsed} | {row.LateHoursRemaining} | {row.Name}");

    if (args.Contains("--export"))
    {
        // Generate the same workbook/PDF the export endpoint serves.
        var exporter = new TabularReportExportService();
        var columns = new[]
        {
            "الرقم المالي", "الاسم", "الإدارة", "المستوى", "المنطقة", "الشهر", "الموعد",
            "أيام التأخير", "دقائق التأخير", "المستخدم من 30 دقيقة", "المتبقي من 30 دقيقة",
            "المستخدم من ساعتين", "المتبقي من ساعتين"
        };
        var exportRows = summary
            .Select(s => (IReadOnlyList<object?>)new object?[]
            {
                s.FinancialNo, s.Name, s.Department, s.Level, s.Area, s.MonthDisplay, s.Schedule,
                s.LateDays, s.LateMinutes, s.GraceUsedMinutes, s.GraceRemainingMinutes,
                s.LateHoursUsed, s.LateHoursRemaining
            })
            .ToList();

        var subtitle = $"{monthStart:yyyy-MM} - عدد الموظفين: {summary.Count}";
        var xlsx = exporter.GenerateExcel("ملخص استخدام سماحات التأخير", subtitle, "ملخص السماحات", columns, exportRows, true);
        var pdf = exporter.GeneratePdf("ملخص استخدام سماحات التأخير", subtitle, columns, exportRows, true);

        var outDir = Path.GetFullPath(Path.Combine("..", "..", "outputs"));
        Directory.CreateDirectory(outDir);
        var xlsxPath = Path.Combine(outDir, $"allowance_summary_{monthStart:yyyyMM}_test.xlsx");
        var pdfPath = Path.Combine(outDir, $"allowance_summary_{monthStart:yyyyMM}_test.pdf");
        await File.WriteAllBytesAsync(xlsxPath, xlsx);
        await File.WriteAllBytesAsync(pdfPath, pdf);
        Console.WriteLine();
        Console.WriteLine($"Excel: {xlsxPath} ({xlsx.Length:N0} bytes, {exportRows.Count} rows)");
        Console.WriteLine($"PDF  : {pdfPath} ({pdf.Length:N0} bytes)");
    }

    return;
}

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

internal sealed class SimpleDbContextFactory(DbContextOptions<AppDbContext> options)
    : IDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext() => new(options);
}
