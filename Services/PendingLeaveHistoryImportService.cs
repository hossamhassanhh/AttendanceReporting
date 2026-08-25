using AttendanceApp.Data;
using AttendanceApp.Models;
using ExcelDataReader;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Text;

namespace AttendanceApp.Services;

public sealed class PendingLeaveHistoryImportService : IHostedService
{
    private const string PendingFileName = "PendingLeaveHistoryImport.xlsx";
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PendingLeaveHistoryImportService> _logger;

    public PendingLeaveHistoryImportService(IServiceScopeFactory scopeFactory, ILogger<PendingLeaveHistoryImportService> logger)
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
            var result = await ImportAsync(pendingPath, cancellationToken);
            var archiveDirectory = Path.Combine(importDirectory, "Applied");
            Directory.CreateDirectory(archiveDirectory);
            var archivePath = Path.Combine(archiveDirectory, $"LeaveHistoryImport-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx");
            File.Move(pendingPath, archivePath, overwrite: false);
            WriteReport(importDirectory, result);
            _logger.LogInformation("Imported {Total} leave records from {File} (Inserted {Inserted}, Skipped {Skipped})", result.TotalRows, PendingFileName, result.Inserted, result.Skipped);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to import pending leave history workbook");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task<ImportResult> ImportAsync(string path, CancellationToken cancellationToken)
    {
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = false }
        });

        if (dataSet.Tables.Count == 0)
            throw new InvalidDataException("Leave history workbook has no worksheets.");

        var sheetMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["عارضه"] = "C",
            ["اعتيادي"] = "A",
            ["بدل راحه"] = "E",
            ["بدل راحه "] = "E",
            ["بدل راحة"] = "E",
            ["بدل عطله"] = "H",
            ["بدل عطله "] = "H",
            ["بدل عطلة"] = "H"
        };

        using var scope = _scopeFactory.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        using var db = await factory.CreateDbContextAsync(cancellationToken);
        var leaveTypes = await db.LeaveTypes.ToDictionaryAsync(t => t.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var calendarSettings = await db.AttendanceDaySettings.AsNoTracking().ToListAsync(cancellationToken);

        var total = 0;
        var inserted = 0;
        var skipped = 0;
        var missingEmployees = 0;

        // Cache existing employees for quick check
        var allFinancialNos = await db.Employees.Select(e => e.FinancialNo).ToListAsync(cancellationToken);
        var employeeSet = new HashSet<string>(allFinancialNos, StringComparer.OrdinalIgnoreCase);

        // For duplicate check, we will query per batch
        foreach (DataTable table in dataSet.Tables)
        {
            var rawName = table.TableName?.Trim() ?? string.Empty;
            var normalized = NormalizeArabic(rawName);
            if (!sheetMap.TryGetValue(rawName.Trim(), out var code) && !sheetMap.TryGetValue(normalized, out code))
            {
                // Fallback by normalized without spaces
                var cleaned = rawName.Replace(" ", "").Trim();
                if (!sheetMap.TryGetValue(cleaned, out code))
                {
                    _logger.LogWarning("Unknown sheet {Sheet} skipped", rawName);
                    continue;
                }
            }

            if (!leaveTypes.TryGetValue(code, out var leaveType))
            {
                _logger.LogWarning("Leave type {Code} not found for sheet {Sheet}", code, rawName);
                continue;
            }

            var rows = ExtractRows(table, rawName);
            _logger.LogInformation("Sheet {Sheet} ({Code}) has {Count} data rows", rawName, code, rows.Count);

            // Process in batches of 1000 to avoid memory pressure
            const int batchSize = 1000;
            for (int batchStart = 0; batchStart < rows.Count; batchStart += batchSize)
            {
                var batch = rows.Skip(batchStart).Take(batchSize).ToList();
                total += batch.Count;

                // Cache existing for this leave type batch
                var finNosInBatch = batch.Select(b => b.FinancialNo).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var fromMin = batch.Min(b => b.FromDate);
                var toMax = batch.Max(b => b.ToDate);
                var existingInBatch = await db.LeaveTransactions
                    .Where(t => t.LeaveTypeId == leaveType.Id
                        && finNosInBatch.Contains(t.EmployeeFinancialNo)
                        && t.FromDate >= fromMin && t.ToDate <= toMax)
                    .Select(t => new { t.EmployeeFinancialNo, t.FromDate, t.ToDate })
                    .ToListAsync(cancellationToken);
                var existingSet = new HashSet<string>(existingInBatch.Select(e => $"{e.EmployeeFinancialNo}|{e.FromDate:yyyy-MM-dd}|{e.ToDate:yyyy-MM-dd}"), StringComparer.OrdinalIgnoreCase);

                var toAdd = new List<LeaveTransaction>();
                foreach (var r in batch)
                {
                    if (!employeeSet.Contains(r.FinancialNo))
                    {
                        missingEmployees++;
                        skipped++;
                        continue;
                    }

                    var key = $"{r.FinancialNo}|{r.FromDate:yyyy-MM-dd}|{r.ToDate:yyyy-MM-dd}";
                    if (existingSet.Contains(key))
                    {
                        skipped++;
                        continue;
                    }

                    // Verify not already in toAdd (duplicates within file)
                    if (toAdd.Any(t => t.EmployeeFinancialNo == r.FinancialNo && t.FromDate == r.FromDate && t.ToDate == r.ToDate))
                    {
                        skipped++;
                        continue;
                    }

                    // Use sheet's DaysCount, but also calculate actual days for validation
                    var days = r.DaysCount;
                    if (days <= 0)
                    {
                        // Fallback to calculated
                        days = CountActualLeaveDays(r.FromDate, r.ToDate, calendarSettings);
                        if (days <= 0) days = (r.ToDate - r.FromDate).Days + 1;
                    }

                    toAdd.Add(new LeaveTransaction
                    {
                        EmployeeFinancialNo = r.FinancialNo,
                        LeaveTypeId = leaveType.Id,
                        FromDate = r.FromDate,
                        ToDate = r.ToDate,
                        DaysCount = days,
                        Reason = null,
                        Status = "Approved",
                        EnteredBy = "admin",
                        CreatedAt = DateTime.Now
                    });
                    existingSet.Add(key);
                }

                if (toAdd.Count > 0)
                {
                    db.LeaveTransactions.AddRange(toAdd);
                    await db.SaveChangesAsync(cancellationToken);
                    inserted += toAdd.Count;
                }

                // Clear change tracker to free memory
                db.ChangeTracker.Clear();
            }
        }

        return new ImportResult(total, inserted, skipped, missingEmployees);
    }

    private static List<ParsedRow> ExtractRows(DataTable table, string sheetName)
    {
        var list = new List<ParsedRow>();
        // Determine start row: find header row containing "رقم مالي" or "id"
        int headerRow = -1;
        for (int r = 0; r < Math.Min(table.Rows.Count, 5); r++)
        {
            var row = table.Rows[r];
            for (int c = 0; c < table.Columns.Count; c++)
            {
                var val = row[c]?.ToString()?.Trim() ?? string.Empty;
                var norm = NormalizeArabic(val);
                if (norm == "رقم مالي" || norm == "رقم مالي " || val.Trim().Equals("id", StringComparison.OrdinalIgnoreCase))
                {
                    headerRow = r;
                    break;
                }
            }
            if (headerRow >= 0) break;
        }

        int startRow = headerRow >= 0 ? headerRow + 1 : 0;
        // Column mapping per sheet
        bool isHoliday = NormalizeArabic(sheetName) == "بدل عطله" || sheetName.Trim().Equals("بدل عطله", StringComparison.OrdinalIgnoreCase);
        int finCol = 0, fromCol = 1, toCol = 2, daysCol = 3;

        for (int r = startRow; r < table.Rows.Count; r++)
        {
            var row = table.Rows[r];
            var finRaw = row[finCol]?.ToString()?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(finRaw)) continue;
            // Skip header-like
            if (NormalizeArabic(finRaw) == "رقم مالي" || finRaw.Equals("id", StringComparison.OrdinalIgnoreCase)) continue;
            // FinancialNo should be numeric
            if (!IsNumeric(finRaw)) continue;

            var fromRaw = row[fromCol];
            var toRaw = row[toCol];
            var daysRaw = row[daysCol];

            var fromDate = ParseDate(fromRaw);
            var toDate = ParseDate(toRaw);
            if (!fromDate.HasValue || !toDate.HasValue) continue;
            if (toDate.Value < fromDate.Value) continue;

            double days = 0;
            if (daysRaw != null && daysRaw != DBNull.Value)
            {
                if (daysRaw is double d) days = d;
                else if (daysRaw is int i) days = i;
                else if (double.TryParse(daysRaw.ToString(), out var parsed)) days = parsed;
            }
            if (days <= 0) days = (toDate.Value - fromDate.Value).Days + 1;

            list.Add(new ParsedRow(finRaw, fromDate.Value.Date, toDate.Value.Date, days));
        }
        return list;
    }

    private static bool IsNumeric(string s) => s.All(c => char.IsDigit(c)) && s.Length > 0 && s.Length <= 10;

    private static DateTime? ParseDate(object? value)
    {
        if (value == null || value == DBNull.Value) return null;
        if (value is DateTime dt) return dt.Date;
        if (value is double oa) // OADate
        {
            try { return DateTime.FromOADate(oa).Date; } catch { }
        }
        var str = value.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(str)) return null;
        if (DateTime.TryParse(str, out var parsed)) return parsed.Date;
        return null;
    }

    private static string NormalizeArabic(string value) =>
        value.Trim()
            .Replace('أ', 'ا')
            .Replace('إ', 'ا')
            .Replace('آ', 'ا')
            .Replace('ى', 'ي')
            .Replace('ة', 'ه')
            .Replace(" ", "")
            .ToLowerInvariant();

    private static double CountActualLeaveDays(DateTime fromDate, DateTime toDate, IReadOnlyCollection<AttendanceDaySetting> calendarSettings)
    {
        var days = 0;
        for (var d = fromDate.Date; d <= toDate.Date; d = d.AddDays(1))
        {
            var status = AttendanceCalendarRules.GetDayStatus(d, calendarSettings);
            if (status != AttendanceCalendarRules.WeeklyRest && status != AttendanceCalendarRules.Holiday)
                days++;
        }
        return days;
    }

    private static void WriteReport(string importDirectory, ImportResult result)
    {
        var reportDir = Path.Combine(importDirectory, "Reports");
        Directory.CreateDirectory(reportDir);
        File.WriteAllLines(Path.Combine(reportDir, "leave-history-import-report.txt"),
            new[]
            {
                $"GeneratedAt: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                $"TotalRows: {result.TotalRows}",
                $"Inserted: {result.Inserted}",
                $"Skipped: {result.Skipped}",
                $"MissingEmployees: {result.MissingEmployees}"
            }, Encoding.UTF8);
    }

    private sealed record ParsedRow(string FinancialNo, DateTime FromDate, DateTime ToDate, double DaysCount);
    private sealed record ImportResult(int TotalRows, int Inserted, int Skipped, int MissingEmployees);
}
