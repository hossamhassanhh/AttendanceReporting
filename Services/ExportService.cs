using AttendanceApp.Data;
using AttendanceApp.Models;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace AttendanceApp.Services;

public class ExportService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private const string MonthlyAttendanceTemplatePath = @"D:\Applications\AttendanceReporting\Input\Monthly Attendance Template.xlsx";
    private const string MonthlyAttendanceTemplateFileName = "Monthly Attendance Template.xlsx";

    private static readonly Dictionary<string, string> StatusToCode = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Present", "X" },
        { "Checked In", "X" },
        { "Missing Check Out", "B" },
        { "Late", "X" },
        { "Early Leave", "B" },
        { "Absent", "B" },
        { "Leave", "A" },
        { "Work From Home", "WH" },
        { "Weekly Rest", "R" },
        { "Holiday", "H" },
        { "Mission", "DX" },
        { "Training", "T" },
    };

    private static readonly Dictionary<string, string> LeaveTypeCodeMap = new(StringComparer.OrdinalIgnoreCase)
{
        { "A", "A" }, { "Regular", "A" }, { "Regular Leave", "A" },
        { "S", "S" }, { "Sick", "S" }, { "Sick Leave", "S" },
        { "C", "C" }, { "Casual", "C" }, { "Casual Leave", "C" },
        { "B", "B" }, { "Absence", "B" },
        { "E", "E" }, { "Rest", "E" }, { "Rest Allowance", "E" },
        { "H", "E" },
        { "DI", "DI" }, { "External Mission", "DI" },
        { "DX", "DX" }, { "Internal Mission", "DX" },
        { "T", "T" }, { "Training", "T" },
        { "P", "P" }, { "Permission", "P" },
        { "PL", "X1" }, { "Late Permission", "X1" },
        { "PE", "X1" }, { "Early Leave Permission", "X1" },
        { "W", "R" }, { "Weekly Rest", "R" },
    };

    public ExportService(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<List<string>> GetDepartmentsAsync()
    {
        using var db = await _factory.CreateDbContextAsync();
        return await db.Employees
            .AsNoTracking()
            // Retain departments assigned to inactive employees for historic reporting.
            .Where(e => e.Department != null && e.Department != "")
            .Select(e => e.Department!)
            .Distinct()
            .OrderBy(d => d)
            .ToListAsync();
    }

    public async Task<ExportFilterOptions> GetFilterOptionsAsync()
    {
        using var db = await _factory.CreateDbContextAsync();
        var departments = await db.Employees.AsNoTracking()
            .Where(e => e.Department != null && e.Department != "")
            .Select(e => e.Department!)
            .Distinct()
            .OrderBy(d => d)
            .ToListAsync();
        var levels = await db.Employees.AsNoTracking()
            .Where(e => e.Level != null && e.Level != "")
            .Select(e => e.Level!)
            .Distinct()
            .OrderBy(l => l)
            .ToListAsync();
        var areas = await db.Employees.AsNoTracking()
            .Where(e => e.WorkLocation != null && e.WorkLocation != "")
            .Select(e => e.WorkLocation!)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync();
        return new ExportFilterOptions
        {
            Departments = departments,
            Levels = levels,
            Areas = areas
        };
    }

    public async Task<byte[]> GenerateMonthlySheetAsync(int year, int month, string? department, string? level = null, string? area = null)
    {
        using var db = await _factory.CreateDbContextAsync();

        var departmentTerm = department?.Trim();
        var levelTerm = level?.Trim();
        var areaTerm = area?.Trim();
        var levelIsStandardGroup = AttendanceStatusRules.IsStandardJobLevelsGroup(levelTerm);
        var monthStart = new DateTime(year, month, 1);
        var monthEnd = monthStart.AddMonths(1);

        var employees = await db.Employees
            .AsNoTracking()
            .Where(e => e.IsActive
                && (string.IsNullOrWhiteSpace(departmentTerm) || (e.Department != null && e.Department.Contains(departmentTerm)))
                && (string.IsNullOrWhiteSpace(levelTerm) || (e.Level != null && (levelIsStandardGroup ? AttendanceStatusRules.StandardJobLevels.Contains(e.Level!) : e.Level == levelTerm)))
                && (string.IsNullOrWhiteSpace(areaTerm) || (e.WorkLocation != null && e.WorkLocation == areaTerm)))
            .OrderBy(e => e.FinancialNo)
            .ToListAsync();

        var dailyAtt = await db.DailyAttendances
            .AsNoTracking()
            .Include(d => d.LeaveType)
            .Where(d => d.Date >= monthStart && d.Date < monthEnd)
            .ToListAsync();

        var calendarSettings = await db.AttendanceDaySettings
            .Where(s => s.Date >= monthStart && s.Date < monthEnd)
            .AsNoTracking()
            .ToListAsync();

        var leaveAtt = await GetLeaveCodesAsync(db, year, month);

        var monthlyAtt = await db.MonthlyAttendances
            .AsNoTracking()
            .Where(a => a.Year == year && a.Month == month)
            .ToListAsync();

        var attByEmpDate = dailyAtt
            .GroupBy(d => d.EmployeeFinancialNo)
            .ToDictionary(g => g.Key, g => g.ToDictionary(d => d.Date.Day));

        var monthlyByEmpDate = monthlyAtt
            .GroupBy(a => a.EmployeeFinancialNo)
            .ToDictionary(g => g.Key, g => g.ToDictionary(a => a.Day));

        var monthNames = new[] { "", "يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو", "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر" };
        var daysInMonth = DateTime.DaysInMonth(year, month);

        var exportEmployees = employees
            .Where(emp => attByEmpDate.ContainsKey(emp.FinancialNo)
                || monthlyByEmpDate.ContainsKey(emp.FinancialNo)
                || leaveAtt.ContainsKey(emp.FinancialNo)
                || HasConfiguredCalendarDay(year, month, calendarSettings))
            .ToList();
        return GenerateMonthlySheetFromTemplate(year, month, department, monthNames[month], daysInMonth,
            exportEmployees, attByEmpDate, monthlyByEmpDate, leaveAtt, calendarSettings);
    }

    public async Task<List<object>> GetPreviewDataAsync(int year, int month, string? department, string? level = null, string? area = null)
    {
        using var db = await _factory.CreateDbContextAsync();

        var departmentTerm = department?.Trim();
        var levelTerm = level?.Trim();
        var areaTerm = area?.Trim();
        var levelIsStandardGroup = AttendanceStatusRules.IsStandardJobLevelsGroup(levelTerm);
        var monthStart = new DateTime(year, month, 1);
        var monthEnd = monthStart.AddMonths(1);

        var employees = await db.Employees
            .AsNoTracking()
            .Where(e => e.IsActive
                && (string.IsNullOrWhiteSpace(departmentTerm) || (e.Department != null && e.Department.Contains(departmentTerm)))
                && (string.IsNullOrWhiteSpace(levelTerm) || (e.Level != null && (levelIsStandardGroup ? AttendanceStatusRules.StandardJobLevels.Contains(e.Level!) : e.Level == levelTerm)))
                && (string.IsNullOrWhiteSpace(areaTerm) || (e.WorkLocation != null && e.WorkLocation == areaTerm)))
            .OrderBy(e => e.FinancialNo)
            .ToListAsync();

        var dailyAtt = await db.DailyAttendances
            .AsNoTracking()
            .Include(d => d.LeaveType)
            .Where(d => d.Date >= monthStart && d.Date < monthEnd)
            .ToListAsync();

        var calendarSettings = await db.AttendanceDaySettings
            .Where(s => s.Date >= monthStart && s.Date < monthEnd)
            .AsNoTracking()
            .ToListAsync();

        var leaveAtt = await GetLeaveCodesAsync(db, year, month);

        var monthlyAtt = await db.MonthlyAttendances
            .AsNoTracking()
            .Where(a => a.Year == year && a.Month == month)
            .ToListAsync();

        var attByEmpDate = dailyAtt
            .GroupBy(d => d.EmployeeFinancialNo)
            .ToDictionary(g => g.Key, g => g.ToDictionary(d => d.Date.Day));

        var monthlyByEmpDate = monthlyAtt
            .GroupBy(a => a.EmployeeFinancialNo)
            .ToDictionary(g => g.Key, g => g.ToDictionary(a => a.Day));

        var daysInMonth = DateTime.DaysInMonth(year, month);
        var result = new List<object>();

        foreach (var emp in employees)
        {
            var hasAtt = attByEmpDate.ContainsKey(emp.FinancialNo)
                || monthlyByEmpDate.ContainsKey(emp.FinancialNo)
                || leaveAtt.ContainsKey(emp.FinancialNo)
                || HasConfiguredCalendarDay(year, month, calendarSettings);
            if (!hasAtt) continue;

            var codes = new string[daysInMonth];
            int presentDays = 0, regLeave = 0, sickDays = 0, casualDays = 0;
            int absenceDays = 0, restDays = 0, extMission = 0, intMission = 0, trainingDays = 0;

            var permissions = new PermissionTracker();
            for (int d = 1; d <= daysInMonth; d++)
            {
                var empAtts = attByEmpDate.GetValueOrDefault(emp.FinancialNo);
                var empMons = monthlyByEmpDate.GetValueOrDefault(emp.FinancialNo);
                var empLeaves = leaveAtt.GetValueOrDefault(emp.FinancialNo);
                var code = GetMonthlyCode(year, month, d, empAtts, empMons, empLeaves, calendarSettings, permissions);

                codes[d - 1] = code;
                if (!string.IsNullOrEmpty(code))
                    CountCode(code, ref presentDays, ref regLeave, ref sickDays,
                        ref casualDays, ref absenceDays, ref restDays,
                        ref extMission, ref intMission, ref trainingDays);
            }

            result.Add(new
            {
                emp.FinancialNo,
                emp.Name,
                emp.JobTitle,
                emp.Department,
                emp.Level,
                DailyCodes = codes,
                PresentDays = presentDays,
                RegularLeave = regLeave,
                SickDays = sickDays,
                CasualDays = casualDays,
                AbsenceDays = absenceDays,
                RestDays = restDays,
                ExternalMission = extMission,
                InternalMission = intMission,
                TrainingDays = trainingDays
            });
        }

        return result;
    }

    public async Task<byte[]> GenerateMonthlyPdfAsync(int year, int month, string? department, string? level = null, string? area = null, string? language = null)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var previewData = await GetPreviewDataAsync(year, month, department, level, area);
        var isArabic = !string.Equals(language, "en", StringComparison.OrdinalIgnoreCase);
        var monthNames = isArabic
            ? new[] { "", "يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو", "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر" }
            : new[] { "", "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var monthName = monthNames[month];
        var departmentText = string.IsNullOrWhiteSpace(department) ? "جميع الإدارات" : department.Trim();
        var areaText = string.IsNullOrWhiteSpace(area) ? "المركز الرئيسى" : area.Trim();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A2.Landscape());
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontFamily("Cambria"));

                page.Header().Column(headerCol =>
                {
                    headerCol.Spacing(3);
                    headerCol.Item().Text($"time sheet periood  {monthName} {year}")
                        .FontSize(14).Bold().AlignCenter();
                    headerCol.Item().AlignCenter().Text(txt =>
                    {
                        txt.Span("Dep : ").FontSize(10).Bold();
                        txt.Span(departmentText).FontSize(10).Bold();
                        txt.Span("          ").FontSize(10);
                        txt.Span("Loction : " + areaText).FontSize(10).Bold();
                    });
                });

                var content = page.Content();
                if (isArabic) content = content.ContentFromRightToLeft();

                content.Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(40);
                        columns.ConstantColumn(110);
                        columns.ConstantColumn(90);
                        for (int d = 0; d < 31; d++)
                            columns.ConstantColumn(22);
                        columns.ConstantColumn(55);
                        columns.ConstantColumn(60);
                        for (int c = 0; c < 9; c++)
                            columns.ConstantColumn(28);
                        columns.ConstantColumn(90);
                        columns.ConstantColumn(80);
                        columns.ConstantColumn(60);
                    });

                    table.Header(header =>
                    {
                        var legendLabels = new[]
                        {
                            "أيام حضور", "اجازة اعتيادى", "أيام مرضى", "أيام عارضه", "غياب",
                            "راحه", "مأمورية خارجية", "مأمورية داخلية", "دورة تدريب"
                        };

                        // Legend row (matches the Excel template row 6)
                        for (int c = 0; c < 3; c++)
                            BorderCell(header.Cell());
                        for (int d = 0; d < 31; d++)
                            BorderCell(header.Cell());
                        BorderCell(header.Cell());
                        BorderCell(header.Cell());
                        foreach (var label in legendLabels)
                            BorderCell(header.Cell()).Padding(1).Text(label).FontSize(7).Bold().AlignCenter();
                        for (int c = 0; c < 3; c++)
                            BorderCell(header.Cell());

                        // Header row (matches the Excel template row 7)
                        BorderHeaderCell(header.Cell()).Text("PR").FontSize(8).Bold().AlignCenter();
                        BorderHeaderCell(header.Cell()).Text("Name").FontSize(8).Bold().AlignCenter();
                        BorderHeaderCell(header.Cell()).Text("Job Title").FontSize(8).Bold().AlignCenter();
                        for (int d = 1; d <= 31; d++)
                            BorderHeaderCell(header.Cell()).Text(d <= daysInMonth ? d.ToString() : string.Empty).FontSize(8).Bold().AlignCenter();
                        BorderHeaderCell(header.Cell()).Text("Total Working Days").FontSize(7).Bold().AlignCenter();
                        BorderHeaderCell(header.Cell()).Text("Employee Signature").FontSize(7).Bold().AlignCenter();
                        foreach (var h in new[] { "X", "A", "S", "C", "B", "E", "DI", "DX", "T" })
                            BorderHeaderCell(header.Cell()).Text(h).FontSize(8).Bold().AlignCenter();
                        BorderHeaderCell(header.Cell()).Text("الادارة العامة").FontSize(8).Bold().AlignCenter();
                        BorderHeaderCell(header.Cell()).Text("المستوى الوظيفى").FontSize(8).Bold().AlignCenter();
                        BorderHeaderCell(header.Cell()).Text("Box").FontSize(8).Bold().AlignCenter();
                    });

                    foreach (var emp in previewData)
                    {
                        var finNo = GetPreviewValue(emp, "FinancialNo")?.ToString() ?? "";
                        var name = GetPreviewValue(emp, "Name")?.ToString() ?? "";
                        var jobTitle = GetPreviewValue(emp, "JobTitle")?.ToString() ?? "";
                        var dept = GetPreviewValue(emp, "Department")?.ToString() ?? "";
                        var level = GetPreviewValue(emp, "Level")?.ToString() ?? "";
                        var dailyCodes = (string[])(GetPreviewValue(emp, "DailyCodes") ?? Array.Empty<string>());
                        var presentDays = Convert.ToInt32(GetPreviewValue(emp, "PresentDays") ?? 0);
                        var regLeave = Convert.ToInt32(GetPreviewValue(emp, "RegularLeave") ?? 0);
                        var sickDays = Convert.ToInt32(GetPreviewValue(emp, "SickDays") ?? 0);
                        var casualDays = Convert.ToInt32(GetPreviewValue(emp, "CasualDays") ?? 0);
                        var absenceDays = Convert.ToInt32(GetPreviewValue(emp, "AbsenceDays") ?? 0);
                        var restDays = Convert.ToInt32(GetPreviewValue(emp, "RestDays") ?? 0);
                        var extMission = Convert.ToInt32(GetPreviewValue(emp, "ExternalMission") ?? 0);
                        var intMission = Convert.ToInt32(GetPreviewValue(emp, "InternalMission") ?? 0);
                        var trainingDays = Convert.ToInt32(GetPreviewValue(emp, "TrainingDays") ?? 0);

                        BorderCell(table.Cell()).Text(finNo).FontSize(7).AlignCenter();
                        BorderCell(table.Cell()).Text(name).FontSize(7);
                        BorderCell(table.Cell()).Text(jobTitle).FontSize(7);

                        for (int d = 1; d <= 31; d++)
                        {
                            var code = d <= daysInMonth && d <= dailyCodes.Length ? dailyCodes[d - 1] : string.Empty;
                            var (fill, font) = GetPdfCodeColor(code, d, year, month, daysInMonth);
                            BorderCell(table.Cell()).Background(fill)
                                .Text(code).FontSize(7).FontColor(font).AlignCenter()
                                .Bold();
                        }

                        BorderCell(table.Cell()).Text(presentDays.ToString()).FontSize(7).AlignCenter();
                        BorderCell(table.Cell());
                        BorderCell(table.Cell()).Text(presentDays.ToString()).FontSize(7).AlignCenter();
                        BorderCell(table.Cell()).Text(regLeave.ToString()).FontSize(7).AlignCenter();
                        BorderCell(table.Cell()).Text(sickDays.ToString()).FontSize(7).AlignCenter();
                        BorderCell(table.Cell()).Text(casualDays.ToString()).FontSize(7).AlignCenter();
                        BorderCell(table.Cell()).Text(absenceDays.ToString()).FontSize(7).AlignCenter();
                        BorderCell(table.Cell()).Text(restDays.ToString()).FontSize(7).AlignCenter();
                        BorderCell(table.Cell()).Text(extMission.ToString()).FontSize(7).AlignCenter();
                        BorderCell(table.Cell()).Text(intMission.ToString()).FontSize(7).AlignCenter();
                        BorderCell(table.Cell()).Text(trainingDays.ToString()).FontSize(7).AlignCenter();
                        BorderCell(table.Cell()).Text(dept).FontSize(7);
                        BorderCell(table.Cell()).Text(level).FontSize(7);
                        BorderCell(table.Cell());
                    }
                });

                page.Footer().AlignCenter().Text(txt =>
                {
                    txt.Span(isArabic ? "صفحة " : "Page ").FontSize(8);
                    txt.CurrentPageNumber().FontSize(8);
                    txt.Span(isArabic ? " من " : " of ").FontSize(8);
                    txt.TotalPages().FontSize(8);
                });
            });
        });

        using var ms = new MemoryStream();
        document.GeneratePdf(ms);
        return ms.ToArray();
    }

    private static IContainer BorderCell(IContainer cell)
    {
        return cell.Border(0.5f).BorderColor("#C9CED6");
    }

    private static IContainer BorderHeaderCell(IContainer cell)
    {
        return cell.Border(0.5f).BorderColor("#C9CED6").Background("#D9E2F3");
    }

    private static (string Fill, string Font) GetPdfCodeColor(string code, int day, int year, int month, int daysInMonth)
    {
        var normalized = (code ?? string.Empty).Trim().ToUpperInvariant();
        string? fill = normalized switch
        {
            var c when c.StartsWith("B") => "#FF0000",
            var c when c.StartsWith("S") => "#00B050",
            var c when c.StartsWith("C") => "#FFC000",
            var c when c.StartsWith("A") => "#FFFF00",
            var c when c == "P" => "#F4B183",
            var c when c.StartsWith("E") => "#B4C6E7",
            var c when c.StartsWith("DI") || c.StartsWith("DX") => "#00B0F0",
            var c when c.StartsWith("T") => "#BFBFBF",
            var c when c == "R" || c.StartsWith("W") || c.StartsWith("H") => "#D9D9D9",
            var c when c.StartsWith("X1") => "#DDEBF7",
            var c when c.StartsWith("X2") => "#00B0F0",
            var c when c.StartsWith("X3") => "#5B9BD5",
            var c when c.StartsWith("X4") => "#7030A0",
            var c when c.StartsWith("X") => "#DDEBF7", // Default for X1, X2, X3, etc.
            _ => null
        };

        if (fill == null)
        {
            var isWeekend = day > daysInMonth;
            if (day <= daysInMonth)
                isWeekend = new DateTime(year, month, day).DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday;
            fill = isWeekend ? "#BFBFBF" : "#FFFFFF";
        }

        return (fill, normalized.StartsWith("B") ? "#FFFFFF" : "#000000");
    }

    private static string GetCodeFromDaily(DailyAttendance da)
    {
        if (da.Status == "Leave" && da.LeaveType != null)
        {
            var ltCode = da.LeaveType.Code ?? "";
            if (LeaveTypeCodeMap.TryGetValue(ltCode, out var mapped))
                return mapped;
            return ltCode;
        }

        if (StatusToCode.TryGetValue(da.Status, out var code))
            return code;

        return "X";
    }

    private static async Task<Dictionary<string, Dictionary<int, string>>> GetLeaveCodesAsync(AppDbContext db, int year, int month)
    {
        var monthStart = new DateTime(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var transactions = await db.LeaveTransactions
            .AsNoTracking()
            .Include(t => t.LeaveType)
            .Where(t => t.Status == "Approved" && t.FromDate <= monthEnd && t.ToDate >= monthStart)
            .ToListAsync();

        var result = new Dictionary<string, Dictionary<int, string>>();
        foreach (var transaction in transactions)
        {
            var code = GetCodeFromLeaveType(transaction.LeaveType);
            var from = transaction.FromDate.Date < monthStart ? monthStart : transaction.FromDate.Date;
            var to = transaction.ToDate.Date > monthEnd ? monthEnd : transaction.ToDate.Date;

            if (!result.TryGetValue(transaction.EmployeeFinancialNo, out var days))
            {
                days = new Dictionary<int, string>();
                result[transaction.EmployeeFinancialNo] = days;
            }

            for (var date = from; date <= to; date = date.AddDays(1))
                days[date.Day] = code;
        }

        return result;
    }

    private static string GetCodeFromLeaveType(LeaveType? leaveType)
    {
        if (leaveType == null)
            return "A";

        var ltCode = leaveType.Code ?? string.Empty;
        if (LeaveTypeCodeMap.TryGetValue(ltCode, out var mapped))
            return mapped;

        return string.IsNullOrWhiteSpace(ltCode) ? "A" : ltCode;
    }

    private static string GetMonthlyCode(
        int year,
        int month,
        int day,
        Dictionary<int, DailyAttendance>? dailyByDay,
        Dictionary<int, MonthlyAttendance>? monthlyByDay,
        Dictionary<int, string>? leaveByDay,
        IReadOnlyCollection<AttendanceDaySetting>? calendarSettings,
        PermissionTracker? permissions = null)
    {
        if (leaveByDay != null && leaveByDay.TryGetValue(day, out var leaveCode))
            return NormalizeMonthlyCode(leaveCode);

        var calendarCode = AttendanceCalendarRules.GetMonthlyCode(new DateTime(year, month, day), calendarSettings);
        if (!string.IsNullOrWhiteSpace(calendarCode))
            return calendarCode;

        if (dailyByDay != null && dailyByDay.TryGetValue(day, out var da))
        {
            if (permissions != null && string.Equals(da.Status, "Late", StringComparison.OrdinalIgnoreCase))
            {
                var lateMins = AttendanceStatusRules.GetLateMinutes(da);
                if (permissions.TryConsumeGraceLate(lateMins))
                    return "X";
                if (permissions.TryConsumeLate(lateMins))
                {
                    // Return X{hours} based on hours consumed for this day (min X1)
                    var hoursConsumed = Math.Max(1, (lateMins + 59) / 60);
                    return $"X{hoursConsumed}";
                }
                return "B";
            }

            if (permissions != null
                && string.Equals(da.Status, "Early Leave", StringComparison.OrdinalIgnoreCase)
                && da.LastPunch.HasValue
                && permissions.TryConsumeEarlyLeave(AttendanceStatusRules.ToEgyptTime(da.LastPunch.Value).TimeOfDay))
            {
                return "X1";
            }

            return GetCodeFromDaily(da);
        }

        if (monthlyByDay != null && monthlyByDay.TryGetValue(day, out var ma))
            return NormalizeMonthlyCode(ma.Code);

        return string.Empty;
    }

    private static string NormalizeMonthlyCode(string? code)
    {
        var normalized = (code ?? string.Empty).Trim().ToUpperInvariant();
        return normalized == "W" ? "R" : normalized;
    }

    private static bool HasConfiguredCalendarDay(int year, int month, IReadOnlyCollection<AttendanceDaySetting>? calendarSettings)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        for (var day = 1; day <= daysInMonth; day++)
        {
            if (AttendanceCalendarRules.GetMonthlyCode(new DateTime(year, month, day), calendarSettings) != null)
                return true;
        }

        return false;
    }

    private static object? GetPreviewValue(object source, string propertyName)
    {
        return source.GetType().GetProperty(propertyName)?.GetValue(source);
    }

    private static byte[] GenerateMonthlySheetWithExcel(
        int year,
        int month,
        string? department,
        string monthName,
        int daysInMonth,
        List<Employee> exportEmployees,
        Dictionary<string, Dictionary<int, DailyAttendance>> attByEmpDate,
        Dictionary<string, Dictionary<int, MonthlyAttendance>> monthlyByEmpDate,
        Dictionary<string, Dictionary<int, string>> leaveByEmpDate)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Excel template export requires Windows and Microsoft Excel.");

        var templatePath = ResolveMonthlyAttendanceTemplatePath();
        if (!File.Exists(templatePath))
            throw new FileNotFoundException("Monthly report template file was not found.", templatePath);

        var tempInput = Path.Combine(Path.GetTempPath(), $"monthly-attendance-template-{Guid.NewGuid():N}.xlsx");
        File.Copy(templatePath, tempInput, overwrite: true);

        dynamic? excel = null;
        dynamic? workbook = null;
        dynamic? worksheet = null;

        try
        {
            var excelType = Type.GetTypeFromProgID("Excel.Application")
                ?? throw new InvalidOperationException("Microsoft Excel is not installed or COM automation is unavailable.");

            excel = Activator.CreateInstance(excelType)!;
            excel.Visible = false;
            excel.DisplayAlerts = false;
            excel.EnableEvents = false;
            excel.ScreenUpdating = false;
            excel.Calculation = -4135; // xlCalculationManual

            workbook = excel.Workbooks.Open(tempInput, 0, false);
            worksheet = workbook.Worksheets.Item(1);

            worksheet.Cells[1, 2].Value2 = $"time sheet periood  {monthName} {year}";
            worksheet.Cells[2, 3].Value2 = string.IsNullOrWhiteSpace(department) ? "تنمية الاعمال" : department;
            worksheet.Cells[2, 11].Value2 = "Dep :";
            worksheet.Cells[2, 13].Value2 = "Loction : المركز الرئيسى";

            for (var d = 1; d <= 31; d++)
                worksheet.Cells[7, 3 + d].Value2 = d <= daysInMonth ? d.ToString() : "";

            var usedRange = worksheet.UsedRange;
            var lastRow = usedRange.Row + usedRange.Rows.Count - 1;
            ReleaseComObject(usedRange);

            var templateLastDataRow = GetLastTemplateEmployeeRow(worksheet, lastRow);
            ResizeTemplateDataRows(worksheet, 8, templateLastDataRow, exportEmployees.Count);

            var row = 8;
            var dayStyleGroups = new Dictionary<DayCellStyle, List<string>>();
            foreach (var emp in exportEmployees)
            {
                var employeeInfo = new object[1, 3];
                employeeInfo[0, 0] = emp.FinancialNo;
                employeeInfo[0, 1] = emp.Name;
                employeeInfo[0, 2] = emp.JobTitle ?? "";
                worksheet.Range(worksheet.Cells[row, 1], worksheet.Cells[row, 3]).Value2 = employeeInfo;

                int presentDays = 0, regLeave = 0, sickDays = 0, casualDays = 0;
                int absenceDays = 0, restDays = 0, extMission = 0, intMission = 0, trainingDays = 0;

                var dayCodes = new object[1, 31];
                var permissions = new PermissionTracker();
                for (var d = 1; d <= 31; d++)
                {
                    var empAtts = attByEmpDate.GetValueOrDefault(emp.FinancialNo);
                    var empMons = monthlyByEmpDate.GetValueOrDefault(emp.FinancialNo);
                    var empLeaves = leaveByEmpDate.GetValueOrDefault(emp.FinancialNo);
                    var code = d <= daysInMonth
                        ? GetMonthlyCode(year, month, d, empAtts, empMons, empLeaves, null, permissions)
                        : string.Empty;

                    dayCodes[0, d - 1] = code;
                    if (!string.IsNullOrEmpty(code))
                        CountCode(code, ref presentDays, ref regLeave, ref sickDays,
                            ref casualDays, ref absenceDays, ref restDays,
                            ref extMission, ref intMission, ref trainingDays);
                }

                dynamic dayRange = worksheet.Range(worksheet.Cells[row, 4], worksheet.Cells[row, 34]);
                var mergeValue = dayRange.MergeCells;
                var isMerged = mergeValue is bool b && b;
                if (!isMerged)
                    dayRange.Value2 = dayCodes;
                ReleaseComObject(dayRange);

                QueueDayCodeColors(dayStyleGroups, row, dayCodes, year, month, daysInMonth);

                worksheet.Cells[row, 35].Value2 = presentDays;

                var summary = new object[1, 11];
                summary[0, 0] = presentDays;
                summary[0, 1] = regLeave;
                summary[0, 2] = sickDays;
                summary[0, 3] = casualDays;
                summary[0, 4] = absenceDays;
                summary[0, 5] = restDays;
                summary[0, 6] = extMission;
                summary[0, 7] = intMission;
                summary[0, 8] = trainingDays;
                summary[0, 9] = emp.Department ?? "";
                summary[0, 10] = emp.Level ?? "";
                worksheet.Range(worksheet.Cells[row, 37], worksheet.Cells[row, 47]).Value2 = summary;

                row++;
            }

            ApplyQueuedDayColors(worksheet, dayStyleGroups);

            workbook.Save();
            workbook.Close(false);
            excel.Quit();

            return File.ReadAllBytes(tempInput);
        }
        finally
        {
            ReleaseComObject(worksheet);
            ReleaseComObject(workbook);
            if (excel != null)
            {
                try { excel.Quit(); } catch { }
                ReleaseComObject(excel);
            }

            try { File.Delete(tempInput); } catch { }
        }
    }

    private static byte[] GenerateMonthlySheetWithClosedXml(
        int year,
        int month,
        string? department,
        string monthName,
        int daysInMonth,
        List<Employee> exportEmployees,
        Dictionary<string, Dictionary<int, DailyAttendance>> attByEmpDate,
        Dictionary<string, Dictionary<int, MonthlyAttendance>> monthlyByEmpDate,
        Dictionary<string, Dictionary<int, string>> leaveByEmpDate,
        IReadOnlyCollection<AttendanceDaySetting> calendarSettings)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Monthly Attendance");

        ws.RightToLeft = true;

        // Row 1: Title
        ws.Cell(1, 1).Value = $"time sheet period {monthName} {year}";
        ws.Range(1, 1, 1, 48).Merge();
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(1, 1).Style.Font.FontName = "Cambria";
        ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // Row 2: First set of labels
        ws.Cell(2, 1).Value = "Annual";
        ws.Cell(2, 2).Value = "Casual";
        ws.Cell(2, 3).Value = "Rest";
        ws.Cell(2, 4).Value = "Holiday";
        ws.Cell(2, 5).Value = "";
        ws.Cell(2, 6).Value = "From";
        ws.Cell(2, 7).Value = "Work";
        ws.Cell(2, 8).Value = "Home";
        ws.Cell(2, 9).Value = "Military";
        ws.Cell(2, 10).Value = "Infection";
        ws.Cell(2, 11).Value = "Dep :";
        ws.Cell(2, 12).Value = string.IsNullOrWhiteSpace(department) ? "تنمية الاعمال" : department;
        ws.Range(2, 12, 2, 14).Merge();
        ws.Cell(2, 15).Value = "Loection :";
        ws.Cell(2, 16).Value = "المركز الرئيسى";
        ws.Range(2, 16, 2, 18).Merge();
        ws.Cell(2, 19).Value = "تقرير رئيسي";

        // Row 3: First set of codes
        ws.Cell(3, 1).Value = "A";
        ws.Cell(3, 2).Value = "C";
        ws.Cell(3, 3).Value = "R";
        ws.Cell(3, 4).Value = "H";
        ws.Cell(3, 5).Value = "";
        ws.Cell(3, 6).Value = "";
        ws.Cell(3, 7).Value = "WH";
        ws.Cell(3, 8).Value = "";
        ws.Cell(3, 9).Value = "ML";
        ws.Cell(3, 10).Value = "I";

        // Row 4: Second set of labels
        ws.Cell(4, 1).Value = "Maternity";
        ws.Cell(4, 2).Value = "Absent";
        ws.Cell(4, 3).Value = "Sick Leave";
        ws.Cell(4, 4).Value = "Chronic Sick";
        ws.Cell(4, 5).Value = "Vacation";
        ws.Cell(4, 6).Value = "Without";
        ws.Cell(4, 7).Value = "Pay";
        ws.Cell(4, 8).Value = "Labor";
        ws.Cell(4, 9).Value = "Reduction";
        ws.Cell(4, 10).Value = "Others";

        // Row 5: Second set of codes
        ws.Cell(5, 1).Value = "MA";
        ws.Cell(5, 2).Value = "B";
        ws.Cell(5, 3).Value = "S";
        ws.Cell(5, 4).Value = "CS";
        ws.Cell(5, 5).Value = "VW";
        ws.Cell(5, 6).Value = "";
        ws.Cell(5, 7).Value = "";
        ws.Cell(5, 8).Value = "RD";
        ws.Cell(5, 9).Value = "O";

        // Row 6: Third set of labels + codes
        ws.Cell(6, 1).Value = "Training";
        ws.Cell(6, 2).Value = "T";
        ws.Cell(6, 3).Value = "Hajj";
        ws.Cell(6, 4).Value = "HJ";
        ws.Cell(6, 5).Value = "Accident";
        ws.Cell(6, 6).Value = "K";
        ws.Cell(6, 7).Value = "Extraordinary";
        ws.Cell(6, 8).Value = "vacation";
        ws.Cell(6, 9).Value = "EV";

        // Style legend rows
        for (var legendRow = 2; legendRow <= 6; legendRow++)
        {
            var legendRange = ws.Range(legendRow, 1, legendRow, 19);
            legendRange.Style.Font.Bold = true;
            legendRange.Style.Font.FontSize = 8;
            legendRange.Style.Font.FontName = "Cambria";
            legendRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            legendRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            legendRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        // Row 7: Day numbers header
        var dayHeaders = new List<string> { "PR", "Name", "Job Title" };
        for (var d = 1; d <= 31; d++) dayHeaders.Add(d <= daysInMonth ? d.ToString() : string.Empty);
        dayHeaders.AddRange(new[]
        {
            "Total Working Days", "Employee Signature", "X", "A", "S", "C", "B", "E", "DI", "DX", "T",
            "الادارة العامة", "المستوى الوظيفى", "Box"
        });

        for (var c = 0; c < dayHeaders.Count; c++)
            ws.Cell(7, c + 1).Value = dayHeaders[c];

        var headerRange = ws.Range(7, 1, 7, dayHeaders.Count);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Font.FontSize = 8;
        headerRange.Style.Font.FontName = "Cambria";
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9E2F3");
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        var row = 8;
        foreach (var emp in exportEmployees)
        {
            var empAtts = attByEmpDate.GetValueOrDefault(emp.FinancialNo);
            var empMons = monthlyByEmpDate.GetValueOrDefault(emp.FinancialNo);
            var empLeaves = leaveByEmpDate.GetValueOrDefault(emp.FinancialNo);

            // Check for extended leave (all days in month are leave)
            var leaveStartDate = GetLeaveStartDate(emp.FinancialNo, year, month, leaveByEmpDate);
            var lastWorkDay = GetLastWorkDay(emp.FinancialNo, year, month, daysInMonth, attByEmpDate, monthlyByEmpDate, leaveByEmpDate, calendarSettings);

            // Calculate total working days first
            int presentDays = 0, regLeave = 0, sickDays = 0, casualDays = 0;
            int absenceDays = 0, restDays = 0, extMission = 0, intMission = 0, trainingDays = 0;
            var allCodes = new string[31];
            var permissions = new PermissionTracker();
            for (var d = 1; d <= 31; d++)
            {
                var code = d <= daysInMonth
                    ? GetMonthlyCode(year, month, d, empAtts, empMons, empLeaves, calendarSettings, permissions)
                    : string.Empty;
                allCodes[d - 1] = code;
                if (!string.IsNullOrEmpty(code))
                    CountCode(code, ref presentDays, ref regLeave, ref sickDays,
                        ref casualDays, ref absenceDays, ref restDays,
                        ref extMission, ref intMission, ref trainingDays);
            }

            // Check if employee is on extended leave (all days are leave code)
            var isExtendedLeave = leaveStartDate.HasValue && presentDays == 0 && daysInMonth > 0;

            if (isExtendedLeave)
            {
                // Add merged green row for extended leave
                var fromDate = leaveStartDate.Value;
                var toDate = new DateTime(year, month, daysInMonth);
                var leaveText = $"فترة غياب من: {fromDate:dd/MM/yyyy} إلى: {toDate:dd/MM/yyyy}";

                ws.Cell(row, 1).Value = emp.FinancialNo;
                ws.Cell(row, 2).Value = emp.Name;
                ws.Range(row, 3, row, 34).Merge();
                ws.Cell(row, 3).Value = leaveText;
                ws.Cell(row, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(row, 3).Style.Font.Bold = true;
                ws.Cell(row, 3).Style.Font.FontSize = 9;
                ws.Cell(row, 35).Value = 0;

                // Green fill for extended leave row
                var greenFill = XLColor.FromHtml("#00B050");
                var leaveRowRange = ws.Range(row, 1, row, 48);
                leaveRowRange.Style.Fill.BackgroundColor = greenFill;
                leaveRowRange.Style.Font.FontColor = XLColor.White;
                leaveRowRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                leaveRowRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                row++;
            }
            else if (lastWorkDay.HasValue && presentDays == 0 && daysInMonth > 0)
            {
                // Add merged green row for last working day
                var lastDay = lastWorkDay.Value;
                var lastDayText = $"اخر يوم عمل: {lastDay:dd-MM-yyyy}";

                ws.Cell(row, 1).Value = emp.FinancialNo;
                ws.Cell(row, 2).Value = emp.Name;
                ws.Range(row, 3, row, 34).Merge();
                ws.Cell(row, 3).Value = lastDayText;
                ws.Cell(row, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(row, 3).Style.Font.Bold = true;
                ws.Cell(row, 3).Style.Font.FontSize = 9;
                ws.Cell(row, 35).Value = 0;

                // Green fill for last work day row
                var greenFill = XLColor.FromHtml("#00B050");
                var lastDayRowRange = ws.Range(row, 1, row, 48);
                lastDayRowRange.Style.Fill.BackgroundColor = greenFill;
                lastDayRowRange.Style.Font.FontColor = XLColor.White;
                lastDayRowRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                lastDayRowRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                row++;
            }
            else
            {
                // Normal employee row
                ws.Cell(row, 1).Value = emp.FinancialNo;
                ws.Cell(row, 2).Value = emp.Name;
                ws.Cell(row, 3).Value = emp.JobTitle ?? string.Empty;

                for (var d = 1; d <= 31; d++)
                {
                    var code = allCodes[d - 1];
                    var cell = ws.Cell(row, 3 + d);
                    cell.Value = code;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    cell.Style.Font.Bold = !string.IsNullOrWhiteSpace(code);

                    ApplyClosedXmlCodeColor(cell, code, d, year, month, daysInMonth);
                }

                ws.Cell(row, 35).Value = presentDays;
                ws.Cell(row, 37).Value = presentDays;
                ws.Cell(row, 38).Value = regLeave;
                ws.Cell(row, 39).Value = sickDays;
                ws.Cell(row, 40).Value = casualDays;
                ws.Cell(row, 41).Value = absenceDays;
                ws.Cell(row, 42).Value = restDays;
                ws.Cell(row, 43).Value = extMission;
                ws.Cell(row, 44).Value = intMission;
                ws.Cell(row, 45).Value = trainingDays;
                ws.Cell(row, 46).Value = emp.Department ?? string.Empty;
                ws.Cell(row, 47).Value = emp.Level ?? string.Empty;

                var rowRange = ws.Range(row, 1, row, dayHeaders.Count);
                rowRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                rowRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                row++;
            }
        }

        ws.SheetView.FreezeRows(7);
        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] GenerateMonthlySheetFromTemplate(
        int year,
        int month,
        string? department,
        string monthName,
        int daysInMonth,
        List<Employee> exportEmployees,
        Dictionary<string, Dictionary<int, DailyAttendance>> attByEmpDate,
        Dictionary<string, Dictionary<int, MonthlyAttendance>> monthlyByEmpDate,
        Dictionary<string, Dictionary<int, string>> leaveByEmpDate,
        IReadOnlyCollection<AttendanceDaySetting> calendarSettings)
    {
        using var workbook = OpenMonthlyAttendanceTemplate();
        var ws = workbook.Worksheets.First();
        ws.Name = BuildMonthlySheetName(year, month, department);
        ws.RightToLeft = true;

        PrepareTemplateDataRows(ws, exportEmployees.Count);

        ws.Cell(1, 2).Value = $"time sheet periood  {monthName} {year}";
        ws.Cell(2, 3).Value = string.IsNullOrWhiteSpace(department) ? "جميع الإدارات" : department.Trim();
        ws.Cell(2, 11).Value = "Dep : ";
        ws.Cell(2, 13).Value = "Loction : المركز الرئيسى";

        var row = 8;
        foreach (var emp in exportEmployees)
        {
            var empAtts = attByEmpDate.GetValueOrDefault(emp.FinancialNo);
            var empMons = monthlyByEmpDate.GetValueOrDefault(emp.FinancialNo);
            var empLeaves = leaveByEmpDate.GetValueOrDefault(emp.FinancialNo);

            ws.Cell(row, 1).Value = emp.FinancialNo;
            ws.Cell(row, 2).Value = emp.Name;
            ws.Cell(row, 3).Value = emp.JobTitle ?? string.Empty;

            int presentDays = 0, regLeave = 0, sickDays = 0, casualDays = 0;
            int absenceDays = 0, restDays = 0, extMission = 0, intMission = 0, trainingDays = 0;

            var permissions = new PermissionTracker();
            for (var d = 1; d <= 31; d++)
            {
                var code = d <= daysInMonth
                    ? GetMonthlyCode(year, month, d, empAtts, empMons, empLeaves, calendarSettings, permissions)
                    : string.Empty;

                var cell = ws.Cell(row, 3 + d);
                cell.Value = code;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Font.Bold = !string.IsNullOrWhiteSpace(code);
                ApplyClosedXmlCodeColor(cell, code, d, year, month, daysInMonth);

                if (!string.IsNullOrEmpty(code))
                    CountCode(code, ref presentDays, ref regLeave, ref sickDays,
                        ref casualDays, ref absenceDays, ref restDays,
                        ref extMission, ref intMission, ref trainingDays);
            }

            ws.Cell(row, 35).Value = presentDays;
            ws.Cell(row, 37).Value = presentDays;
            ws.Cell(row, 38).Value = regLeave;
            ws.Cell(row, 39).Value = sickDays;
            ws.Cell(row, 40).Value = casualDays;
            ws.Cell(row, 41).Value = absenceDays;
            ws.Cell(row, 42).Value = restDays;
            ws.Cell(row, 43).Value = extMission;
            ws.Cell(row, 44).Value = intMission;
            ws.Cell(row, 45).Value = trainingDays;
            ws.Cell(row, 46).Value = emp.Department ?? string.Empty;
            ws.Cell(row, 47).Value = emp.Level ?? string.Empty;
            row++;
        }

        ws.SheetView.FreezeRows(7);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return RestoreTemplateDrawingParts(stream.ToArray(), ResolveMonthlyAttendanceTemplatePath());
    }

    private static void ApplyClosedXmlCodeColor(IXLCell cell, string code, int day, int year, int month, int daysInMonth)
    {
        var normalized = (code ?? string.Empty).Trim().ToUpperInvariant();
        var fill = normalized switch
        {
            var c when c.StartsWith("B") => XLColor.FromHtml("#FF0000"),
            var c when c.StartsWith("S") => XLColor.FromHtml("#00B050"),
            var c when c.StartsWith("C") => XLColor.FromHtml("#FFC000"),
            var c when c.StartsWith("A") => XLColor.FromHtml("#FFFF00"),
            var c when c == "P" => XLColor.FromHtml("#F4B183"),
            var c when c.StartsWith("E") => XLColor.FromHtml("#B4C6E7"),
            var c when c.StartsWith("DI") || c.StartsWith("DX") => XLColor.FromHtml("#00B0F0"),
            var c when c.StartsWith("T") => XLColor.FromHtml("#BFBFBF"),
            var c when c == "R" || c.StartsWith("W") || c.StartsWith("H") => XLColor.FromHtml("#D9D9D9"),
            var c when c.StartsWith("X1") => XLColor.FromHtml("#DDEBF7"),
            var c when c.StartsWith("X2") => XLColor.FromHtml("#00B0F0"),
            var c when c.StartsWith("X3") => XLColor.FromHtml("#5B9BD5"),
            var c when c.StartsWith("X4") => XLColor.FromHtml("#7030A0"),
            var c when c.StartsWith("X") => XLColor.FromHtml("#DDEBF7"),
            _ => null
        };

        if (fill == null)
        {
            var isWeekend = day > daysInMonth;
            if (day <= daysInMonth)
            {
                var dayOfWeek = new DateTime(year, month, day).DayOfWeek;
                isWeekend = dayOfWeek == DayOfWeek.Friday || dayOfWeek == DayOfWeek.Saturday;
            }

            fill = isWeekend ? XLColor.FromHtml("#BFBFBF") : XLColor.White;
        }

        cell.Style.Fill.BackgroundColor = fill;
        cell.Style.Font.FontColor = normalized.StartsWith("B") ? XLColor.White : XLColor.Black;
    }

    private static void ReleaseComObject(object? value)
    {
        if (value != null && Marshal.IsComObject(value))
            Marshal.ReleaseComObject(value);
    }

    private static int GetLastTemplateEmployeeRow(dynamic worksheet, int usedLastRow)
    {
        var lastDataRow = 7;
        for (var row = 8; row <= usedLastRow; row++)
        {
            var pr = Convert.ToString(worksheet.Cells[row, 1].Value2)?.Trim() ?? string.Empty;
            int parsedPr;
            if (int.TryParse(pr, out parsedPr))
                lastDataRow = row;
        }

        return lastDataRow;
    }

    private static void ResizeTemplateDataRows(dynamic worksheet, int firstDataRow, int templateLastDataRow, int requiredRows)
    {
        var templateRows = Math.Max(0, templateLastDataRow - firstDataRow + 1);

        if (requiredRows < templateRows)
        {
            var deleteFrom = firstDataRow + requiredRows;
            var deleteTo = templateLastDataRow;
            if (deleteFrom <= deleteTo)
            {
                dynamic rows = worksheet.Rows[$"{deleteFrom}:{deleteTo}"];
                rows.Delete();
                ReleaseComObject(rows);
            }
        }
        else if (requiredRows > templateRows && templateRows > 0)
        {
            var rowsToAdd = requiredRows - templateRows;
            var insertAt = templateLastDataRow + 1;
            for (var i = 0; i < rowsToAdd; i++)
            {
                dynamic sourceRow = worksheet.Rows[templateLastDataRow];
                dynamic targetRow = worksheet.Rows[insertAt];
                sourceRow.Copy();
                targetRow.Insert(-4121); // xlShiftDown
                ReleaseComObject(targetRow);
                ReleaseComObject(sourceRow);
            }
        }

        for (var row = firstDataRow; row < firstDataRow + requiredRows; row++)
        {
            if (row != firstDataRow)
                CopyEmployeeRowFormat(worksheet, firstDataRow, row);

            dynamic dataRange = worksheet.Range(worksheet.Cells[row, 1], worksheet.Cells[row, 48]);
            dataRange.ClearContents();
            ReleaseComObject(dataRange);
        }
    }

    private static void CopyEmployeeRowFormat(dynamic worksheet, int sourceRowNumber, int targetRowNumber)
    {
        dynamic sourceRange = worksheet.Range(worksheet.Cells[sourceRowNumber, 1], worksheet.Cells[sourceRowNumber, 48]);
        dynamic targetRange = worksheet.Range(worksheet.Cells[targetRowNumber, 1], worksheet.Cells[targetRowNumber, 48]);

        sourceRange.Copy();
        targetRange.PasteSpecial(-4122); // xlPasteFormats
        worksheet.Application.CutCopyMode = false;

        dynamic sourceRow = worksheet.Rows[sourceRowNumber];
        dynamic targetRow = worksheet.Rows[targetRowNumber];
        targetRow.RowHeight = sourceRow.RowHeight;

        ReleaseComObject(targetRow);
        ReleaseComObject(sourceRow);
        ReleaseComObject(targetRange);
        ReleaseComObject(sourceRange);
    }

    private static void QueueDayCodeColors(Dictionary<DayCellStyle, List<string>> groups, int row, object[,] dayCodes, int year, int month, int daysInMonth)
    {
        for (var d = 1; d <= 31; d++)
        {
            var code = Convert.ToString(dayCodes[0, d - 1])?.Trim() ?? string.Empty;

            var fill = GetCodeFillColor(code);
            if (!fill.HasValue)
            {
                var isWeekend = d > daysInMonth;
                if (d <= daysInMonth)
                {
                    var day = new DateTime(year, month, d).DayOfWeek;
                    isWeekend = day == DayOfWeek.Friday || day == DayOfWeek.Saturday;
                }

                fill = isWeekend ? ExcelColor(191, 191, 191) : ExcelColor(255, 255, 255);
            }

            var font = code.StartsWith("B", StringComparison.OrdinalIgnoreCase)
                ? ExcelColor(255, 255, 255)
                : ExcelColor(0, 0, 0);

            var style = new DayCellStyle(fill.Value, font);
            if (!groups.TryGetValue(style, out var addresses))
            {
                addresses = new List<string>();
                groups[style] = addresses;
            }

            addresses.Add($"{GetColumnName(3 + d)}{row}");
        }
    }

    private static void ApplyQueuedDayColors(dynamic worksheet, Dictionary<DayCellStyle, List<string>> groups)
    {
        foreach (var group in groups)
        {
            foreach (var chunk in group.Value.Chunk(100))
            {
                dynamic range = worksheet.Range(string.Join(",", chunk));
                dynamic interior = range.Interior;
                dynamic font = range.Font;

                interior.Color = group.Key.FillColor;
                font.Color = group.Key.FontColor;

                ReleaseComObject(font);
                ReleaseComObject(interior);
                ReleaseComObject(range);
            }
        }
    }

    private sealed record DayCellStyle(int FillColor, int FontColor);

    private static int? GetCodeFillColor(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        var normalized = code.Trim().ToUpperInvariant();

if (normalized.StartsWith("B")) return ExcelColor(255, 0, 0);
        if (normalized.StartsWith("S")) return ExcelColor(0, 176, 80);
        if (normalized.StartsWith("C")) return ExcelColor(255, 192, 0);
        if (normalized.StartsWith("A")) return ExcelColor(255, 255, 0);
        if (normalized.StartsWith("E")) return ExcelColor(180, 198, 231);
        if (normalized.StartsWith("DI") || normalized.StartsWith("DX")) return ExcelColor(0, 176, 240);
        if (normalized.StartsWith("T")) return ExcelColor(191, 191, 191);
        if (normalized == "R" || normalized.StartsWith("W") || normalized.StartsWith("H")) return ExcelColor(217, 217, 217);
        if (normalized.StartsWith("X1")) return ExcelColor(221, 235, 247);
        if (normalized.StartsWith("X2")) return ExcelColor(0, 176, 240);
        if (normalized.StartsWith("X3")) return ExcelColor(91, 155, 213);
        if (normalized.StartsWith("X4")) return ExcelColor(112, 48, 160);
        if (normalized.StartsWith("X")) return ExcelColor(221, 235, 247); // Default for X1, X2, etc.

        return null;
    }

    private static int ExcelColor(int red, int green, int blue)
    {
        return red + (green << 8) + (blue << 16);
    }

    private static string GetCellText(SheetData sheetData, SharedStringTable? sharedStrings, string columnName, int rowIndex)
    {
        var cell = GetCell(sheetData, columnName, rowIndex, createIfMissing: false);
        if (cell == null)
            return string.Empty;

        var value = cell.CellValue?.Text ?? cell.InnerText ?? string.Empty;
        if (cell.DataType == null)
            return value;

        if (cell.DataType == CellValues.InlineString)
            return cell.InlineString?.Text?.Text ?? cell.InnerText ?? string.Empty;

        if (cell.DataType == CellValues.SharedString
            && int.TryParse(value, out var sharedStringIndex)
            && sharedStrings != null)
            return sharedStrings.ElementAt(sharedStringIndex).InnerText;

        return value;
    }

    private static void SetCellValue(
        SheetData sheetData,
        SharedStringTable sharedStrings,
        string columnName,
        int rowIndex,
        string value,
        List<CellRange>? mergedRanges = null,
        bool preserveMergedValues = false)
    {
        if (preserveMergedValues && mergedRanges != null)
        {
            var columnIndex = GetColumnIndex(columnName);
            if (mergedRanges.Any(r => r.Contains(rowIndex, columnIndex)))
                return;
        }

        var cell = GetCell(sheetData, columnName, rowIndex, createIfMissing: true)!;
        cell.CellFormula = null;
        cell.InlineString = null;
        cell.CellValue = new CellValue(GetSharedStringIndex(sharedStrings, value ?? string.Empty).ToString());
        cell.DataType = CellValues.SharedString;
    }

    private static int GetSharedStringIndex(SharedStringTable sharedStrings, string value)
    {
        var index = 0;
        foreach (var item in sharedStrings.Elements<SharedStringItem>())
        {
            if (item.InnerText == value)
                return index;
            index++;
        }

        sharedStrings.AppendChild(new SharedStringItem(new Text(value)));
        return index;
    }

    private static Cell? GetCell(SheetData sheetData, string columnName, int rowIndex, bool createIfMissing)
    {
        var row = sheetData.Elements<Row>().FirstOrDefault(r => r.RowIndex?.Value == rowIndex);
        if (row == null)
        {
            if (!createIfMissing)
                return null;

            row = new Row { RowIndex = (uint)rowIndex };
            sheetData.Append(row);
        }

        var cellReference = $"{columnName}{rowIndex}";
        var cell = row.Elements<Cell>().FirstOrDefault(c => c.CellReference?.Value == cellReference);
        if (cell != null || !createIfMissing)
            return cell;

        cell = new Cell { CellReference = cellReference };
        var columnIndex = GetColumnIndex(columnName);
        var nextCell = row.Elements<Cell>()
            .FirstOrDefault(c => GetColumnIndexFromCellReference(c.CellReference?.Value) > columnIndex);
        row.InsertBefore(cell, nextCell);
        return cell;
    }

    private static string GetColumnName(int columnNumber)
    {
        var dividend = columnNumber;
        var columnName = string.Empty;

        while (dividend > 0)
        {
            var modulo = (dividend - 1) % 26;
            columnName = Convert.ToChar('A' + modulo) + columnName;
            dividend = (dividend - modulo) / 26;
        }

        return columnName;
    }

    private static int GetColumnIndex(string columnName)
    {
        var sum = 0;
        foreach (var c in columnName)
            sum = sum * 26 + (char.ToUpperInvariant(c) - 'A' + 1);
        return sum;
    }

    private static int GetColumnIndexFromCellReference(string? cellReference)
    {
        if (string.IsNullOrWhiteSpace(cellReference))
            return int.MaxValue;

        var column = new string(cellReference.TakeWhile(char.IsLetter).ToArray());
        return GetColumnIndex(column);
    }

    private static List<CellRange> GetMergedRanges(Worksheet worksheet)
    {
        return worksheet.Elements<MergeCells>()
            .SelectMany(m => m.Elements<MergeCell>())
            .Select(m => CellRange.Parse(m.Reference?.Value))
            .Where(r => r != null)
            .Select(r => r!)
            .ToList();
    }

    private sealed record CellRange(int FirstRow, int LastRow, int FirstColumn, int LastColumn)
    {
        public bool Contains(int row, int column)
        {
            return row >= FirstRow && row <= LastRow && column >= FirstColumn && column <= LastColumn;
        }

        public static CellRange? Parse(string? reference)
        {
            if (string.IsNullOrWhiteSpace(reference))
                return null;

            var parts = reference.Split(':');
            var first = ParseAddress(parts[0]);
            var last = ParseAddress(parts.Length > 1 ? parts[1] : parts[0]);
            return new CellRange(first.Row, last.Row, first.Column, last.Column);
        }

        private static (int Row, int Column) ParseAddress(string address)
        {
            var columnName = new string(address.TakeWhile(char.IsLetter).ToArray());
            var rowText = new string(address.SkipWhile(char.IsLetter).ToArray());
            return (int.Parse(rowText), GetColumnIndex(columnName));
        }
    }

    private static XLWorkbook OpenMonthlyAttendanceTemplate()
    {
        var templatePath = ResolveMonthlyAttendanceTemplatePath();
        if (!File.Exists(templatePath))
            throw new FileNotFoundException("Monthly report template file was not found.", templatePath);

        using var file = new FileStream(templatePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var templateBytes = new MemoryStream();
        file.CopyTo(templateBytes);
        templateBytes.Position = 0;
        return new XLWorkbook(templateBytes);
    }

    private static string ResolveMonthlyAttendanceTemplatePath()
    {
        if (File.Exists(MonthlyAttendanceTemplatePath))
            return MonthlyAttendanceTemplatePath;

        var deployedTemplatePath = Path.Combine(AppContext.BaseDirectory, "Input", MonthlyAttendanceTemplateFileName);
        return File.Exists(deployedTemplatePath) ? deployedTemplatePath : MonthlyAttendanceTemplatePath;
    }

    private static string BuildMonthlySheetName(int year, int month, string? department)
    {
        var departmentPart = string.IsNullOrWhiteSpace(department) ? "All" : department.Trim();
        var name = $"Monthly Attendance {year}-{month:D2} {departmentPart}";
        var invalidChars = new[] { ':', '\\', '/', '?', '*', '[', ']' };
        foreach (var invalid in invalidChars)
            name = name.Replace(invalid, '-');

        name = string.Join(" ", name.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (name.Length > 31)
            name = name[..31].Trim();

        return string.IsNullOrWhiteSpace(name) ? $"Monthly {year}-{month:D2}" : name;
    }

    private static void PrepareTemplateDataRows(IXLWorksheet ws, int requiredRows)
    {
        const int firstDataRow = 8;
        const int templateLastDataRow = 254;
        const int templateFooterStartRow = 255;
        const int reportLastCol = 48;
        const int templateLastCol = 54;
        requiredRows = Math.Max(requiredRows, 1);

        var templateDataRows = templateLastDataRow - firstDataRow + 1;
        if (requiredRows < templateDataRows)
        {
            ws.Rows(firstDataRow + requiredRows, templateLastDataRow)
                .Delete();
        }
        else if (requiredRows > templateDataRows)
        {
            var rowsToAdd = requiredRows - templateDataRows;
            ws.Row(templateFooterStartRow).InsertRowsAbove(rowsToAdd);
            for (var row = templateFooterStartRow; row < templateFooterStartRow + rowsToAdd; row++)
                ws.Range(templateLastDataRow, 1, templateLastDataRow, reportLastCol).CopyTo(ws.Cell(row, 1));
        }

        var cleanTemplateRow = ws.Range(firstDataRow, 1, firstDataRow, reportLastCol);
        var templateRowHeight = ws.Row(firstDataRow).Height;
        for (var row = firstDataRow + 1; row < firstDataRow + requiredRows; row++)
        {
            cleanTemplateRow.CopyTo(ws.Cell(row, 1));
            ws.Row(row).Height = templateRowHeight;
        }

        var lastDataRow = firstDataRow + requiredRows - 1;
        ws.Range(firstDataRow, 1, lastDataRow, reportLastCol)
            .Clear(XLClearOptions.Contents);
        var outsideReport = ws.Range(
            firstDataRow, reportLastCol + 1, lastDataRow, templateLastCol);
        outsideReport.Clear(XLClearOptions.All);
        outsideReport.Style.Fill.PatternType = XLFillPatternValues.None;
        outsideReport.Style.Fill.BackgroundColor = XLColor.NoColor;
    }

    private static byte[] RestoreTemplateDrawingParts(byte[] generatedWorkbook, string templatePath)
    {
        const string drawingPath = "xl/drawings/drawing1.xml";
        const string worksheetPath = "xl/worksheets/sheet1.xml";
        const string worksheetRelationshipsPath = "xl/worksheets/_rels/sheet1.xml.rels";
        const string contentTypesPath = "[Content_Types].xml";
        const string drawingRelationshipType =
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships/drawing";
        const string drawingContentType =
            "application/vnd.openxmlformats-officedocument.drawing+xml";

        byte[] drawingBytes;
        using (var template = ZipFile.OpenRead(templatePath))
        {
            var drawingEntry = template.GetEntry(drawingPath)
                ?? throw new InvalidDataException("The monthly attendance template drawing part is missing.");
            using var drawingStream = drawingEntry.Open();
            var drawingDocument = XDocument.Load(
                drawingStream,
                System.Xml.Linq.LoadOptions.PreserveWhitespace);
            XNamespace drawingText =
                "http://schemas.openxmlformats.org/drawingml/2006/main";
            foreach (var textNode in drawingDocument.Descendants(drawingText + "t")
                .Where(node => string.Equals(node.Value.Trim(), "Previous Annual", StringComparison.OrdinalIgnoreCase)))
            {
                textNode.Value = "Permission";
            }
            using var drawingBuffer = new MemoryStream();
            drawingDocument.Save(drawingBuffer, System.Xml.Linq.SaveOptions.DisableFormatting);
            drawingBytes = drawingBuffer.ToArray();
        }

        using var workbookStream = new MemoryStream();
        workbookStream.Write(generatedWorkbook);
        workbookStream.Position = 0;

        using (var archive = new ZipArchive(workbookStream, ZipArchiveMode.Update, leaveOpen: true))
        {
            ReplaceZipEntry(archive, drawingPath, drawingBytes);

            var relationshipDocument = LoadZipXml(archive, worksheetRelationshipsPath);
            XNamespace packageRelationships =
                "http://schemas.openxmlformats.org/package/2006/relationships";
            var drawingRelationship = relationshipDocument.Root?
                .Elements(packageRelationships + "Relationship")
                .FirstOrDefault(element =>
                    string.Equals((string?)element.Attribute("Type"), drawingRelationshipType,
                        StringComparison.Ordinal));
            var relationshipId = (string?)drawingRelationship?.Attribute("Id") ?? "rId2";
            if (drawingRelationship == null)
            {
                relationshipDocument.Root?.Add(new XElement(
                    packageRelationships + "Relationship",
                    new XAttribute("Id", relationshipId),
                    new XAttribute("Type", drawingRelationshipType),
                    new XAttribute("Target", "../drawings/drawing1.xml")));
                SaveZipXml(archive, worksheetRelationshipsPath, relationshipDocument);
            }

            var worksheetDocument = LoadZipXml(archive, worksheetPath);
            XNamespace spreadsheet =
                "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            XNamespace officeRelationships =
                "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            if (worksheetDocument.Root?.Element(spreadsheet + "drawing") == null)
            {
                var drawing = new XElement(
                    spreadsheet + "drawing",
                    new XAttribute(officeRelationships + "id", relationshipId));
                var followingElement =
                    worksheetDocument.Root?.Element(spreadsheet + "legacyDrawing")
                    ?? worksheetDocument.Root?.Element(spreadsheet + "legacyDrawingHF")
                    ?? worksheetDocument.Root?.Element(spreadsheet + "picture")
                    ?? worksheetDocument.Root?.Element(spreadsheet + "oleObjects")
                    ?? worksheetDocument.Root?.Element(spreadsheet + "extLst");
                if (followingElement != null)
                    followingElement.AddBeforeSelf(drawing);
                else
                    worksheetDocument.Root?.Add(drawing);
                SaveZipXml(archive, worksheetPath, worksheetDocument);
            }

            var contentTypesDocument = LoadZipXml(archive, contentTypesPath);
            XNamespace contentTypes =
                "http://schemas.openxmlformats.org/package/2006/content-types";
            var hasDrawingContentType = contentTypesDocument.Root?
                .Elements(contentTypes + "Override")
                .Any(element =>
                    string.Equals((string?)element.Attribute("PartName"), "/xl/drawings/drawing1.xml",
                        StringComparison.OrdinalIgnoreCase)) == true;
            if (!hasDrawingContentType)
            {
                contentTypesDocument.Root?.Add(new XElement(
                    contentTypes + "Override",
                    new XAttribute("PartName", "/xl/drawings/drawing1.xml"),
                    new XAttribute("ContentType", drawingContentType)));
                SaveZipXml(archive, contentTypesPath, contentTypesDocument);
            }
        }

        return workbookStream.ToArray();
    }

    private static XDocument LoadZipXml(ZipArchive archive, string entryPath)
    {
        var entry = archive.GetEntry(entryPath)
            ?? throw new InvalidDataException($"Workbook package entry is missing: {entryPath}");
        using var stream = entry.Open();
        return XDocument.Load(stream, System.Xml.Linq.LoadOptions.PreserveWhitespace);
    }

    private static void SaveZipXml(ZipArchive archive, string entryPath, XDocument document)
    {
        archive.GetEntry(entryPath)?.Delete();
        var entry = archive.CreateEntry(entryPath, CompressionLevel.Optimal);
        using var stream = entry.Open();
        document.Save(stream, System.Xml.Linq.SaveOptions.DisableFormatting);
    }

    private static void ReplaceZipEntry(ZipArchive archive, string entryPath, byte[] content)
    {
        archive.GetEntry(entryPath)?.Delete();
        var entry = archive.CreateEntry(entryPath, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(content);
    }

    private static void CountCode(string code, ref int present, ref int reg, ref int sick,
        ref int casual, ref int absent, ref int rest, ref int extMission,
        ref int intMission, ref int training)
    {
        switch (code.ToUpperInvariant())
        {
            case "WH": case "X": case "X1": case "X2": case "X3": case "X4": present++; break;
            case "A": reg++; break;
            case "S": sick++; break;
            case "C": casual++; break;
            case "B": absent++; break;
            case "E": rest++; break;
            case "DI": extMission++; break;
            case "DX": intMission++; break;
            case "T": training++; break;
        }
    }

    private static DateTime? GetLeaveStartDate(string financialNo, int year, int month,
        Dictionary<string, Dictionary<int, string>> leaveByEmpDate)
    {
        if (!leaveByEmpDate.TryGetValue(financialNo, out var leaveByDay))
            return null;

        // Find the first day with a leave code
        for (var d = 1; d <= 31; d++)
        {
            if (leaveByDay.TryGetValue(d, out var code) && !string.IsNullOrEmpty(code))
            {
                return new DateTime(year, month, d);
            }
        }

        return null;
    }

    private static DateTime? GetLastWorkDay(string financialNo, int year, int month, int daysInMonth,
        Dictionary<string, Dictionary<int, DailyAttendance>> attByEmpDate,
        Dictionary<string, Dictionary<int, MonthlyAttendance>> monthlyByEmpDate,
        Dictionary<string, Dictionary<int, string>> leaveByEmpDate,
        IReadOnlyCollection<AttendanceDaySetting> calendarSettings)
    {
        DateTime? lastWorkDay = null;

        for (var d = daysInMonth; d >= 1; d--)
        {
            var empAtts = attByEmpDate.GetValueOrDefault(financialNo);
            var empMons = monthlyByEmpDate.GetValueOrDefault(financialNo);
            var empLeaves = leaveByEmpDate.GetValueOrDefault(financialNo);
            var code = GetMonthlyCode(year, month, d, empAtts, empMons, empLeaves, calendarSettings);

            // Consider R, WH as work days
            if (code == "R" || code == "WH")
            {
                lastWorkDay = new DateTime(year, month, d);
                break;
            }
        }

        return lastWorkDay;
    }

    public async Task<List<DailyWagePresentDaysRow>> GetDailyWagePresentDaysAsync(int year, int month)
    {
        using var db = await _factory.CreateDbContextAsync();

        var monthStart = new DateTime(year, month, 1);
        var monthEndExclusive = monthStart.AddMonths(1);
        var daysInMonth = DateTime.DaysInMonth(year, month);

        var employees = await db.Employees
            .AsNoTracking()
            .Where(e => e.ContractType == "مكافأة شاملة يومية")
            .ToListAsync();
        employees = employees
            .OrderBy(e => long.TryParse(e.FinancialNo, out var fin) ? fin : long.MaxValue)
            .ThenBy(e => e.FinancialNo)
            .ToList();

        var dailyAtt = await db.DailyAttendances
            .AsNoTracking()
            .Include(d => d.LeaveType)
            .Where(d => d.Date >= monthStart && d.Date < monthEndExclusive)
            .ToListAsync();

        var calendarSettings = await db.AttendanceDaySettings
            .Where(s => s.Date >= monthStart && s.Date < monthEndExclusive)
            .AsNoTracking()
            .ToListAsync();

        var leaveAtt = await GetLeaveCodesAsync(db, year, month);

        var monthlyAtt = await db.MonthlyAttendances
            .AsNoTracking()
            .Where(a => a.Year == year && a.Month == month)
            .ToListAsync();

        var attByEmpDate = dailyAtt
            .GroupBy(d => d.EmployeeFinancialNo)
            .ToDictionary(g => g.Key, g => g.ToDictionary(d => d.Date.Day));

        var monthlyByEmpDate = monthlyAtt
            .GroupBy(a => a.EmployeeFinancialNo)
            .ToDictionary(g => g.Key, g => g.ToDictionary(a => a.Day));

        var rows = new List<DailyWagePresentDaysRow>();
        foreach (var emp in employees)
        {
            // Same monthly-code pipeline as the monthly sheet (including the
            // late-credit accrual across the month); present = X-family codes.
            var permissions = new PermissionTracker();
            var presentDays = 0;
            for (int d = 1; d <= daysInMonth; d++)
            {
                var code = GetMonthlyCode(year, month, d,
                    attByEmpDate.GetValueOrDefault(emp.FinancialNo),
                    monthlyByEmpDate.GetValueOrDefault(emp.FinancialNo),
                    leaveAtt.GetValueOrDefault(emp.FinancialNo),
                    calendarSettings, permissions);
                if (code == "X" || code == "WH")
                    presentDays++;
            }

            rows.Add(new DailyWagePresentDaysRow
            {
                FinancialNo = emp.FinancialNo,
                Name = emp.Name,
                JobTitle = emp.JobTitle,
                Department = emp.Department,
                Year = year,
                Month = month,
                PresentDays = presentDays
            });
        }

        return rows;
    }
}

public class DailyWagePresentDaysRow
{
    public string FinancialNo { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public string? Department { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public int PresentDays { get; set; }
}

public class ExportFilterOptions
{
    public List<string> Departments { get; set; } = new();
    public List<string> Levels { get; set; } = new();
    public List<string> Areas { get; set; } = new();
}
