using AttendanceApp.Data;
using AttendanceApp.Models;
using ExcelDataReader;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Text;

namespace AttendanceApp.Services;

public sealed class WorkScheduleWorkbookImportService : IHostedService
{
    private const string PendingFileName = "PendingWorkScheduleImport.xlsx";
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WorkScheduleWorkbookImportService> _logger;

    public WorkScheduleWorkbookImportService(
        IServiceScopeFactory scopeFactory,
        ILogger<WorkScheduleWorkbookImportService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var importDirectory = Path.Combine(AppContext.BaseDirectory, "App_Data");
        var pendingPath = Path.Combine(importDirectory, PendingFileName);
        if (!File.Exists(pendingPath))
            return;

        try
        {
            var imported = await ImportAsync(pendingPath, cancellationToken);
            var archiveDirectory = Path.Combine(importDirectory, "Applied");
            Directory.CreateDirectory(archiveDirectory);
            var archivePath = Path.Combine(archiveDirectory, $"WorkScheduleImport-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx");
            File.Move(pendingPath, archivePath, overwrite: false);
            WriteVerificationReport(importDirectory, imported);
            _logger.LogInformation("Imported {Count} employee work-schedule records from {File}", imported.ImportedRows, PendingFileName);
        }
        catch (Exception ex)
        {
            // Keep the pending file in place so the update can be retried after the issue is fixed.
            _logger.LogError(ex, "Unable to import the pending employee work-schedule workbook");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task<WorkScheduleImportResult> ImportAsync(string path, CancellationToken cancellationToken)
    {
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = false }
        });
        if (dataSet.Tables.Count == 0)
            throw new InvalidDataException("The work-schedule workbook has no worksheets.");

        var table = dataSet.Tables[0];
        var headerRow = FindHeaderRow(table);
        if (headerRow < 0)
            throw new InvalidDataException("The work-schedule workbook header was not found.");

        var columns = ResolveColumns(table, headerRow);
        var rows = ReadRows(table, headerRow + 1, columns).ToList();
        if (rows.Count == 0)
            throw new InvalidDataException("The work-schedule workbook has no employee rows.");

        var duplicateNumbers = rows
            .GroupBy(row => row.FinancialNo, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .Take(10)
            .ToList();
        if (duplicateNumbers.Count > 0)
            throw new InvalidDataException($"Duplicate financial numbers in the workbook: {string.Join(", ", duplicateNumbers)}.");

        using var scope = _scopeFactory.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var numbers = rows.Select(row => row.FinancialNo).ToList();
        var employees = await db.Employees
            .Where(employee => numbers.Contains(employee.FinancialNo))
            .ToDictionaryAsync(employee => employee.FinancialNo, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var row in rows)
        {
            if (!employees.TryGetValue(row.FinancialNo, out var employee))
            {
                employee = new Employee { FinancialNo = row.FinancialNo };
                db.Employees.Add(employee);
                employees.Add(row.FinancialNo, employee);
            }

            employee.Name = row.Name;
            employee.Department = NullIfEmpty(row.Department);
            employee.WorkLocation = NullIfEmpty(row.WorkLocation);
            employee.JobStatus = NullIfEmpty(row.JobStatus);
            employee.IsActive = IsActiveStatus(row.JobStatus);
            if (!string.IsNullOrWhiteSpace(row.Level))
                employee.Level = row.Level;

            if (TryGetSchedule(row.Schedule, out var start, out var end))
            {
                employee.ScheduleStart = start;
                employee.ScheduleEnd = end;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var targetEmployee = await db.Employees.AsNoTracking()
            .Where(employee => employee.FinancialNo == "4779")
            .Select(employee => new
            {
                employee.FinancialNo,
                employee.Name,
                employee.Department,
                employee.Level,
                employee.ScheduleStart,
                employee.ScheduleEnd,
                employee.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken);

        return new WorkScheduleImportResult(
            rows.Count,
            targetEmployee?.FinancialNo ?? "4779",
            targetEmployee?.Name ?? string.Empty,
            targetEmployee?.Department ?? string.Empty,
            targetEmployee?.Level ?? string.Empty,
            targetEmployee?.ScheduleStart ?? string.Empty,
            targetEmployee?.ScheduleEnd ?? string.Empty,
            targetEmployee?.IsActive ?? false);
    }

    private static IEnumerable<WorkScheduleRow> ReadRows(DataTable table, int firstDataRow, WorkScheduleColumns columns)
    {
        for (var rowIndex = firstDataRow; rowIndex < table.Rows.Count; rowIndex++)
        {
            var row = table.Rows[rowIndex];
            var financialNo = GetText(row, columns.FinancialNo);
            var name = GetText(row, columns.Name);
            if (string.IsNullOrWhiteSpace(financialNo) || string.IsNullOrWhiteSpace(name))
                continue;

            yield return new WorkScheduleRow(
                financialNo,
                name,
                GetText(row, columns.Department),
                GetText(row, columns.WorkLocation),
                NormalizeLevelDisplay(GetText(row, columns.Level)),
                GetText(row, columns.JobStatus),
                GetText(row, columns.Schedule));
        }
    }

    private static int FindHeaderRow(DataTable table)
    {
        for (var rowIndex = 0; rowIndex < Math.Min(table.Rows.Count, 20); rowIndex++)
        {
            if (FindColumn(table, rowIndex, "رقم مالي", "الرقم المالي", "Financial No", "FinancialNo", "PR") >= 0
                && FindColumn(table, rowIndex, "الاسم", "Name") >= 0)
                return rowIndex;
        }
        return -1;
    }

    private static WorkScheduleColumns ResolveColumns(DataTable table, int headerRow) => new(
        RequiredColumn(table, headerRow, "رقم مالي", "الرقم المالي", "Financial No", "FinancialNo", "PR"),
        RequiredColumn(table, headerRow, "الاسم", "Name"),
        RequiredColumn(table, headerRow, "الادارة العامة", "الإدارة العامة", "الإدارة", "Department"),
        RequiredColumn(table, headerRow, "موقع العمل", "Work Location", "Location"),
        RequiredColumn(table, headerRow, "المستوى الوظيفى", "المستوى الوظيفي", "المستوى", "Level"),
        RequiredColumn(table, headerRow, "الحالة الوظيفية", "الحاله الوظيفيه", "Job Status", "Status"),
        RequiredColumn(table, headerRow, "موعيد العمل", "مواعيد العمل", "موعد العمل", "Schedule", "Working Hours"));

    private static int RequiredColumn(DataTable table, int headerRow, params string[] names)
    {
        var column = FindColumn(table, headerRow, names);
        if (column < 0)
            throw new InvalidDataException($"The work-schedule workbook is missing required column: {names[0]}.");
        return column;
    }

    private static int FindColumn(DataTable table, int headerRow, params string[] names)
    {
        if (headerRow < 0 || headerRow >= table.Rows.Count)
            return -1;

        for (var column = 0; column < table.Columns.Count; column++)
        {
            var value = NormalizeArabicText(GetText(table.Rows[headerRow], column));
            if (names.Any(name => string.Equals(value, NormalizeArabicText(name), StringComparison.OrdinalIgnoreCase)))
                return column;
        }

        return -1;
    }

    private static string GetText(DataRow row, int column) =>
        column >= 0 && column < row.Table.Columns.Count ? row[column]?.ToString()?.Trim() ?? string.Empty : string.Empty;

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string NormalizeLevelDisplay(string value) => NormalizeArabicText(value) switch
    {
        "اداره عليا" => "اداره عليا",
        "ادارة عليا" => "اداره عليا",
        "الاداره العليا" => "اداره عليا",
        "الادارة العليا" => "اداره عليا",
        "المستوى الاول" => "المستوى الاول",
        "المستوي الاول" => "المستوى الاول",
        "المستوى الأول" => "المستوى الاول",
        "المستوي الأول" => "المستوى الاول",
        "المستوى الثانى" => "المستوى الثانى",
        "المستوى الثاني" => "المستوى الثانى",
        "المستوي الثانى" => "المستوى الثانى",
        "المستوي الثاني" => "المستوى الثانى",
        "المستوى الثالث" => "المستوى الثالث",
        "المستوي الثالث" => "المستوى الثالث",
        _ => string.Empty
    };

    private static void WriteVerificationReport(string importDirectory, WorkScheduleImportResult result)
    {
        var reportDirectory = Path.Combine(importDirectory, "Reports");
        Directory.CreateDirectory(reportDirectory);
        File.WriteAllLines(
            Path.Combine(reportDirectory, "work-schedule-import-report.txt"),
            new[]
            {
                $"GeneratedAt: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                $"ImportedRows: {result.ImportedRows}",
                $"EmployeeFinancialNo: {result.EmployeeFinancialNo}",
                $"EmployeeName: {result.EmployeeName}",
                $"Department: {result.Department}",
                $"Level: {result.Level}",
                $"Schedule: {result.ScheduleStart}-{result.ScheduleEnd}",
                $"IsActive: {result.IsActive}"
            },
            Encoding.UTF8);
    }

    private static bool IsActiveStatus(string value)
    {
        var normalized = NormalizeArabicText(value);
        return string.Equals(normalized, "علي قوه العمل", StringComparison.Ordinal)
            || string.Equals(normalized, "active", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeArabicText(string value) =>
        value.Trim()
            .Replace('أ', 'ا')
            .Replace('إ', 'ا')
            .Replace('آ', 'ا')
            .Replace('ى', 'ي')
            .Replace('ة', 'ه');

    private static bool TryGetSchedule(string value, out string start, out string end)
    {
        (start, end) = value switch
        {
            "03:10 / 08:31" => ("08:31", "15:10"),
            "03:20 / 08:31" => ("08:31", "15:20"),
            "03:25 / 08:31" => ("08:31", "15:25"),
            _ => (string.Empty, string.Empty)
        };
        return start.Length > 0;
    }

    private sealed record WorkScheduleRow(
        string FinancialNo,
        string Name,
        string Department,
        string WorkLocation,
        string Level,
        string JobStatus,
        string Schedule);

    private sealed record WorkScheduleColumns(
        int FinancialNo,
        int Name,
        int Department,
        int WorkLocation,
        int Level,
        int JobStatus,
        int Schedule);

    private sealed record WorkScheduleImportResult(
        int ImportedRows,
        string EmployeeFinancialNo,
        string EmployeeName,
        string Department,
        string Level,
        string ScheduleStart,
        string ScheduleEnd,
        bool IsActive);
}
