using AttendanceApp.Services;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AttendanceApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TrackingController : ControllerBase
{
    private readonly AttendanceTrackerService _tracker;
    private readonly TabularReportExportService _reportExporter;

    public TrackingController(
        AttendanceTrackerService tracker,
        TabularReportExportService reportExporter)
    {
        _tracker = tracker;
        _reportExporter = reportExporter;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    [Authorize(Policy = "Attendance")]
    [HttpGet("{financialNo}")]
    public async Task<IActionResult> GetAttendance(string financialNo, [FromQuery] string? date)
    {
        var d = DateTime.TryParse(date, out var dt) ? dt : DateTime.Today;
        var results = await _tracker.GetDailyAttendanceAsync(financialNo, d);
        return Ok(results);
    }

    [Authorize(Policy = "Attendance")]
    [HttpGet("{financialNo}/range")]
    public async Task<IActionResult> GetAttendanceRange(string financialNo, [FromQuery] string? from, [FromQuery] string? to)
    {
        var fromDate = DateTime.TryParse(from, out var fd) ? fd : DateTime.Today.AddDays(-30);
        var toDate = DateTime.TryParse(to, out var td) ? td : DateTime.Today;
        var results = await _tracker.GetEmployeeAttendanceAsync(financialNo, fromDate, toDate);
        return Ok(results);
    }

    [Authorize(Policy = "Attendance")]
    [HttpGet("by-date")]
    public async Task<IActionResult> GetByDate([FromQuery] string? date)
    {
        var d = DateTime.TryParse(date, out var dt) ? dt : DateTime.Today;
        var results = await _tracker.GetAllAttendanceAsync(d);
        return Ok(results);
    }

    private static DateTime? ToEgyptTime(DateTime? utc)
    {
        if (!utc.HasValue) return null;
        return new DateTime(utc.Value.Ticks + TimeSpan.TicksPerHour * 2, DateTimeKind.Unspecified);
    }

    private static DateTime? GetDisplayLastPunch(AttendanceApp.Models.DailyAttendance attendance)
    {
        if (!attendance.FirstPunch.HasValue || !attendance.LastPunch.HasValue)
            return null;

        return attendance.FirstPunch.Value == attendance.LastPunch.Value ? null : attendance.LastPunch;
    }

    [Authorize(Policy = "Attendance")]
    [HttpGet("query")]
    public async Task<IActionResult> Query(
        [FromQuery] string? employees,
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] string? department,
        [FromQuery] string? level,
        [FromQuery] string? area,
        [FromQuery] string? status,
        [FromQuery] string? scheduleStart,
        [FromQuery] string? scheduleEnd)
    {
        var fromDate = DateTime.TryParse(from, out var fd) ? fd : DateTime.Today;
        var toDate = DateTime.TryParse(to, out var td) ? td : DateTime.Today;

        List<string>? empList = null;
        if (!string.IsNullOrWhiteSpace(employees))
            empList = employees.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        var results = await _tracker.QueryAttendanceAsync(
            empList,
            fromDate,
            toDate,
            department,
            level,
            area,
            scheduleStart,
            scheduleEnd);
        var lateAllowance = BuildLateAllowance(results);

        var mapped = results.Select(r =>
        {
            var effectiveStatus = GetEffectiveStatus(r, lateAllowance[r.Id]);
            return new
            {
                r.Id,
                r.EmployeeFinancialNo,
                EmployeeName = r.Employee?.Name ?? "",
                Department = r.Employee?.Department ?? "",
                Level = r.Employee?.Level ?? "",
                r.Date,
                DateDisplay = ToEgyptTime(r.Date)!.Value.ToString("yyyy-MM-dd"),
                FirstPunch = ToEgyptTime(r.FirstPunch),
                LastPunch = ToEgyptTime(GetDisplayLastPunch(r)),
                ScheduledStart = r.Employee?.ScheduleStart,
                ScheduledEnd = r.Employee?.ScheduleEnd,
                Status = effectiveStatus,
                RawStatus = r.Status,
                StatusAr = AttendanceTrackerService.GetStatusArabic(effectiveStatus),
                LateMinutes = lateAllowance[r.Id].DailyMinutes,
                MonthlyLateMinutes = lateAllowance[r.Id].UsedMinutes,
                RemainingLateMinutes = lateAllowance[r.Id].RemainingMinutes
            };
        }).ToList();

        if (!string.IsNullOrWhiteSpace(status))
            mapped = mapped.Where(m => string.Equals(m.Status, status, StringComparison.OrdinalIgnoreCase)).ToList();

        return Ok(mapped);
    }

    [Authorize(Policy = "Attendance")]
    [HttpGet("filter-options")]
    public async Task<IActionResult> GetFilterOptions()
    {
        var options = await _tracker.GetFilterOptionsAsync();
        return Ok(options);
    }

    [Authorize(Policy = "Attendance")]
    [HttpGet("top-management/daily")]
    public async Task<IActionResult> GetTopManagementDaily([FromQuery] string? date)
    {
        var d = DateTime.TryParse(date, out var dt) ? dt : DateTime.Today;
        var rows = await _tracker.GetTopManagementDailyAsync(d);
        return Ok(rows);
    }

    [Authorize(Policy = "Exports")]
    [HttpGet("top-management/daily/export/{format}")]
    public async Task<IActionResult> ExportTopManagementDaily(
        string format,
        [FromQuery] string? date,
        [FromQuery] string? lang)
    {
        if (!IsSupportedExportFormat(format))
            return BadRequest(new { error = "Supported formats are excel and pdf." });

        var reportDate = DateTime.TryParse(date, out var parsedDate) ? parsedDate.Date : DateTime.Today;
        var isArabic = !string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase);
        var dailyRows = await _tracker.GetTopManagementDailyAsync(reportDate);
        var columns = isArabic
            ? new[] { "الرقم المالي", "الاسم", "المسمى الوظيفي", "الإدارة", "انصراف اليوم السابق", "حضور اليوم", "ملاحظات" }
            : new[] { "Financial No", "Name", "Job Title", "Department", "Previous Day Check Out", "Today Check In", "Notes" };
        var rows = dailyRows.Select(row => (IReadOnlyList<object?>)new object?[]
        {
            row.FinancialNo,
            row.Name,
            row.JobTitle,
            row.Department,
            row.YesterdayLastPunch?.ToString("HH:mm:ss") ?? "-",
            row.TodayFirstPunch?.ToString("HH:mm:ss") ?? "-",
            BuildDailyNotes(row, isArabic)
        }).ToList();
        var title = isArabic ? "التقرير اليومي للإدارة العليا" : "Top Management Daily Report";
        var subtitle = isArabic ? $"تاريخ التقرير: {reportDate:yyyy-MM-dd}" : $"Report date: {reportDate:yyyy-MM-dd}";
        return CreateTabularExport(
            format,
            title,
            subtitle,
            isArabic ? "التقرير اليومي" : "Daily Report",
            columns,
            rows,
            isArabic ? "التقرير_اليومي" : "daily_report",
            reportDate,
            isArabic);
    }

    [Authorize(Policy = "Exports")]
    [HttpGet("export/excel")]
    public async Task<IActionResult> ExportExcel(
        [FromQuery] string? employees,
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] string? department,
        [FromQuery] string? level,
        [FromQuery] string? area,
        [FromQuery] string? status,
        [FromQuery] string? scheduleStart,
        [FromQuery] string? scheduleEnd)
    {
        var fromDate = DateTime.TryParse(from, out var fd) ? fd : DateTime.Today;
        var toDate = DateTime.TryParse(to, out var td) ? td : DateTime.Today;

        List<string>? empList = null;
        if (!string.IsNullOrWhiteSpace(employees))
            empList = employees.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        var results = await _tracker.QueryAttendanceAsync(
            empList,
            fromDate,
            toDate,
            department,
            level,
            area,
            scheduleStart,
            scheduleEnd);
        var lateAllowance = BuildLateAllowance(results);
        var egyptZone = TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time");

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Attendance");

        ws.Cell(1, 1).Value = "تقرير الحضور";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(2, 1).Value = $"من {fromDate:yyyy-MM-dd} إلى {toDate:yyyy-MM-dd}";

        ws.Cell(4, 1).Value = "الرقم المالي";
        ws.Cell(4, 2).Value = "الاسم";
        ws.Cell(4, 3).Value = "الإدارة";
        ws.Cell(4, 4).Value = "المستوى";
        ws.Cell(4, 5).Value = "التاريخ";
        ws.Cell(4, 6).Value = "الحالة";
        ws.Cell(4, 7).Value = "الحضور";
        ws.Cell(4, 8).Value = "الانصراف";
        ws.Cell(4, 9).Value = "الموعد";
        ws.Cell(4, 10).Value = "دقائق التأخير";
        ws.Cell(4, 11).Value = "المتبقي من 30 دقيقة";

        var headerRange = ws.Range(4, 1, 4, 11);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(0xD9D9D9);
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        int row = 5;
        foreach (var r in results)
        {
            var effectiveStatus = GetEffectiveStatus(r, lateAllowance[r.Id]);
            if (!string.IsNullOrWhiteSpace(status)
                && !string.Equals(effectiveStatus, status, StringComparison.OrdinalIgnoreCase))
                continue;

            ws.Cell(row, 1).Value = r.EmployeeFinancialNo;
            ws.Cell(row, 2).Value = r.Employee?.Name ?? "";
            ws.Cell(row, 3).Value = r.Employee?.Department ?? "";
            ws.Cell(row, 4).Value = r.Employee?.Level ?? "";
            ws.Cell(row, 5).Value = ToEgyptTime(r.Date)!.Value.ToString("yyyy-MM-dd");
            ws.Cell(row, 6).Value = AttendanceTrackerService.GetStatusArabic(
                effectiveStatus);
            ws.Cell(row, 7).Value = r.FirstPunch.HasValue ? ToEgyptTime(r.FirstPunch)!.Value.ToString("HH:mm:ss") : "-";
            var displayLastPunch = GetDisplayLastPunch(r);
            ws.Cell(row, 8).Value = displayLastPunch.HasValue ? ToEgyptTime(displayLastPunch)!.Value.ToString("HH:mm:ss") : "-";
            var masterSchedule = string.IsNullOrWhiteSpace(r.Employee?.ScheduleStart) || string.IsNullOrWhiteSpace(r.Employee?.ScheduleEnd)
                ? "-"
                : $"{r.Employee.ScheduleStart} - {r.Employee.ScheduleEnd}";
            ws.Cell(row, 9).Value = masterSchedule;
            ws.Cell(row, 10).Value = lateAllowance[r.Id].DailyMinutes;
            ws.Cell(row, 11).Value = lateAllowance[r.Id].RemainingMinutes;

            ws.Range(row, 1, row, 11).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(row, 1, row, 11).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            row++;
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"attendance_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.xlsx");
    }

    [Authorize(Policy = "Exports")]
    [HttpGet("export/pdf")]
    public async Task<IActionResult> ExportPdf(
        [FromQuery] string? employees,
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] string? department,
        [FromQuery] string? level,
        [FromQuery] string? area,
        [FromQuery] string? status,
        [FromQuery] string? scheduleStart,
        [FromQuery] string? scheduleEnd,
        [FromQuery] string? lang)
    {
        var fromDate = DateTime.TryParse(from, out var fd) ? fd : DateTime.Today;
        var toDate = DateTime.TryParse(to, out var td) ? td : DateTime.Today;

        List<string>? empList = null;
        if (!string.IsNullOrWhiteSpace(employees))
            empList = employees.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        var results = await _tracker.QueryAttendanceAsync(
            empList,
            fromDate,
            toDate,
            department,
            level,
            area,
            scheduleStart,
            scheduleEnd);
        var lateAllowance = BuildLateAllowance(results);
        var isArabic = !string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase);

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(20);
                page.Size(PageSizes.A4.Landscape());

                var title = page.Header().Text(isArabic ? "تقرير الحضور" : "Attendance Report")
                    .FontSize(16).Bold().FontColor("#1A2744");
                if (isArabic) title.AlignRight();
                else title.AlignLeft();
                var content = page.Content();
                if (isArabic) content = content.ContentFromRightToLeft();
                content.Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(50);
                        c.RelativeColumn(2);
                        c.ConstantColumn(80);
                        c.ConstantColumn(65);
                        c.ConstantColumn(80);
                        c.ConstantColumn(70);
                        c.ConstantColumn(70);
                        c.ConstantColumn(70);
                        c.ConstantColumn(90);
                        c.ConstantColumn(65);
                        c.ConstantColumn(75);
                    });

                    table.Header(header =>
                    {
                        var headers = isArabic
                            ? new[] { "الرقم المالي", "الاسم", "الإدارة", "المستوى", "التاريخ", "الحالة", "الحضور", "الانصراف", "الموعد", "دقائق التأخير", "المتبقي" }
                            : new[] { "Financial No", "Name", "Department", "Level", "Date", "Status", "Check In", "Check Out", "Schedule", "Late Min.", "Remaining" };
                        foreach (var label in headers)
                            header.Cell()
                                .Background("#1A2744")
                                .Border(0.5f)
                                .BorderColor("#A8B7CA")
                                .Padding(4)
                                .Text(label)
                                .Bold()
                                .FontColor("#FFFFFF")
                                .FontSize(8);
                    });

                    foreach (var r in results)
                    {
                        var effectiveStatus = GetEffectiveStatus(r, lateAllowance[r.Id]);
                        if (!string.IsNullOrWhiteSpace(status)
                            && !string.Equals(effectiveStatus, status, StringComparison.OrdinalIgnoreCase))
                            continue;

                        var dateStr = ToEgyptTime(r.Date)!.Value.ToString("yyyy-MM-dd");
                        var firstStr = r.FirstPunch.HasValue ? ToEgyptTime(r.FirstPunch)!.Value.ToString("HH:mm:ss") : "-";
                        var displayLastPunch = GetDisplayLastPunch(r);
                        var lastStr = displayLastPunch.HasValue ? ToEgyptTime(displayLastPunch)!.Value.ToString("HH:mm:ss") : "-";
                        var schedStr = string.IsNullOrWhiteSpace(r.Employee?.ScheduleStart) || string.IsNullOrWhiteSpace(r.Employee?.ScheduleEnd)
                            ? "-"
                            : $"{r.Employee.ScheduleStart} - {r.Employee.ScheduleEnd}";

                        table.Cell().BorderBottom(0.5f).BorderColor("#D7E0EA").Padding(3).Text(r.EmployeeFinancialNo).FontSize(8);
                        table.Cell().BorderBottom(0.5f).BorderColor("#D7E0EA").Padding(3).Text(r.Employee?.Name ?? "").FontSize(8);
                        table.Cell().BorderBottom(0.5f).BorderColor("#D7E0EA").Padding(3).Text(r.Employee?.Department ?? "").FontSize(8);
                        table.Cell().BorderBottom(0.5f).BorderColor("#D7E0EA").Padding(3).Text(r.Employee?.Level ?? "").FontSize(8);
                        table.Cell().BorderBottom(0.5f).BorderColor("#D7E0EA").Padding(3).Text(dateStr).FontSize(8);
                        table.Cell().BorderBottom(0.5f).BorderColor("#D7E0EA").Padding(3).Text(isArabic ? AttendanceTrackerService.GetStatusArabic(effectiveStatus) : effectiveStatus).FontSize(8);
                        table.Cell().BorderBottom(0.5f).BorderColor("#D7E0EA").Padding(3).Text(firstStr).FontSize(8);
                        table.Cell().BorderBottom(0.5f).BorderColor("#D7E0EA").Padding(3).Text(lastStr).FontSize(8);
                        table.Cell().BorderBottom(0.5f).BorderColor("#D7E0EA").Padding(3).Text(schedStr).FontSize(8);
                        table.Cell().BorderBottom(0.5f).BorderColor("#D7E0EA").Padding(3).Text(lateAllowance[r.Id].DailyMinutes.ToString()).FontSize(8);
                        table.Cell().BorderBottom(0.5f).BorderColor("#D7E0EA").Padding(3).Text(lateAllowance[r.Id].RemainingMinutes.ToString()).FontSize(8);
                    }
                });
            });
        });

        using var ms = new MemoryStream();
        doc.GeneratePdf(ms);
        return File(ms.ToArray(), "application/pdf", $"attendance_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.pdf");
    }

    private static Dictionary<int, LateAllowanceSnapshot> BuildLateAllowance(
        IEnumerable<AttendanceApp.Models.DailyAttendance> records)
    {
        const int monthlyAllowanceMinutes = 30;
        var result = new Dictionary<int, LateAllowanceSnapshot>();
        var usedByEmployeeMonth = new Dictionary<(string FinancialNo, int Year, int Month), int>();

        foreach (var record in records
            .OrderBy(item => item.EmployeeFinancialNo)
            .ThenBy(item => item.Date)
            .ThenBy(item => item.Id))
        {
            var dailyMinutes = string.Equals(record.Status, "Late", StringComparison.OrdinalIgnoreCase)
                ? CalculateLateMinutes(record)
                : 0;
            var key = (record.EmployeeFinancialNo, record.Date.Year, record.Date.Month);
            var used = usedByEmployeeMonth.GetValueOrDefault(key) + dailyMinutes;
            usedByEmployeeMonth[key] = used;
            result[record.Id] = new LateAllowanceSnapshot(
                dailyMinutes,
                used,
                Math.Max(0, monthlyAllowanceMinutes - used));
        }

        return result;
    }

    private static int CalculateLateMinutes(AttendanceApp.Models.DailyAttendance record)
    {
        return AttendanceStatusRules.GetLateMinutes(record);
    }

    private static string GetEffectiveStatus(
        AttendanceApp.Models.DailyAttendance record,
        LateAllowanceSnapshot allowance)
    {
        return string.Equals(record.Status, "Late", StringComparison.OrdinalIgnoreCase)
            && allowance.UsedMinutes <= 30
                ? "Present"
                : record.Status;
    }

    private sealed record LateAllowanceSnapshot(
        int DailyMinutes,
        int UsedMinutes,
        int RemainingMinutes);

    [Authorize(Policy = "Balances")]
    [HttpGet("{financialNo}/balances")]
    public async Task<IActionResult> GetBalances(string financialNo)
    {
        var balances = await _tracker.GetLeaveBalancesAsync(financialNo);
        return Ok(balances);
    }

    [Authorize(Policy = "Balances")]
    [HttpGet("balances")]
    public async Task<IActionResult> GetAllBalances([FromQuery] string? financialNo)
    {
        var balances = await _tracker.GetLeaveBalancesAsync(financialNo);
        return Ok(balances);
    }

    [Authorize(Policy = "Balances")]
    [Authorize(Policy = "Exports")]
    [HttpGet("balances/export/{format}")]
    public async Task<IActionResult> ExportBalances(
        string format,
        [FromQuery] string? financialNo,
        [FromQuery] string? lang)
    {
        if (!IsSupportedExportFormat(format))
            return BadRequest(new { error = "Supported formats are excel and pdf." });

        var isArabic = !string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase);
        var balances = await _tracker.GetLeaveBalancesAsync(financialNo);
        var columns = isArabic
            ? new[] { "السنة", "الرقم المالي", "الاسم", "الإجازة الاعتيادية", "الإجازة العارضة", "بدل الراحة", "بدل العطلة" }
            : new[] { "Year", "Financial No", "Name", "Regular Leave", "Casual Leave", "Rest Allowance", "Holiday Allowance" };
        var rows = balances.Select(balance => (IReadOnlyList<object?>)new object?[]
        {
            balance.Year,
            balance.EmployeeFinancialNo,
            balance.Employee?.Name,
            balance.RegularLeave,
            balance.CasualLeave,
            balance.RestAllowance,
            balance.HolidayAllowance
        }).ToList();
        return CreateTabularExport(
            format,
            isArabic ? "تقرير أرصدة الإجازات" : "Leave Balances Report",
            null,
            isArabic ? "أرصدة الإجازات" : "Leave Balances",
            columns,
            rows,
            isArabic ? "تقرير_أرصدة_الإجازات" : "leave_balances_report",
            DateTime.Today,
            isArabic);
    }

    [Authorize(Policy = "ManageCalendar")]
    [HttpPost("recalculate-statuses")]
    public async Task<IActionResult> RecalculateStatuses([FromBody] AttendanceRecalculationRequest request)
    {
        try
        {
            var employeeTerms = string.IsNullOrWhiteSpace(request.Employees)
                ? null
                : request.Employees.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            var result = await _tracker.RecalculateStatusesAsync(
                request.From,
                request.To,
                employeeTerms,
                request.Department,
                request.Level,
                request.Area,
                request.ScheduleStart,
                request.ScheduleEnd);
            return Ok(result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [Authorize(Policy = "Attendance")]
    [HttpPost("bulk-update")]
    public async Task<IActionResult> BulkUpdate([FromBody] BulkUpdateRequest request)
    {
        try
        {
            var result = await _tracker.BulkUpdateStatusAsync(request.Date, request.Updates);
            return Ok(new { message = result });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private IActionResult CreateTabularExport(
        string format,
        string title,
        string? subtitle,
        string sheetName,
        IReadOnlyList<string> columns,
        IReadOnlyList<IReadOnlyList<object?>> rows,
        string fileStem,
        DateTime reportDate,
        bool isArabic)
    {
        var isPdf = string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase);
        var data = isPdf
            ? _reportExporter.GeneratePdf(title, subtitle, columns, rows, isArabic)
            : _reportExporter.GenerateExcel(title, subtitle, sheetName, columns, rows, isArabic);
        var extension = isPdf ? "pdf" : "xlsx";
        var contentType = isPdf
            ? "application/pdf"
            : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        return File(data, contentType, $"{fileStem}_{reportDate:yyyyMMdd}.{extension}");
    }

    private static bool IsSupportedExportFormat(string format) =>
        string.Equals(format, "excel", StringComparison.OrdinalIgnoreCase)
        || string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase);

    private static string BuildDailyNotes(TopManagementDailyRow row, bool isArabic)
    {
        var notes = new List<string>();
        if (row.MissingYesterdayCheckout)
            notes.Add(isArabic ? "لم يسجل انصراف اليوم السابق" : "Previous-day check-out is missing");
        if (row.MissingTodayCheckIn)
            notes.Add(isArabic ? "لم يسجل حضور اليوم" : "Today's check-in is missing");
        return string.Join(isArabic ? " - " : "; ", notes);
    }
}

public class BulkUpdateRequest
{
    public DateTime Date { get; set; }
    public List<BulkStatusUpdate> Updates { get; set; } = new();
}

public class AttendanceRecalculationRequest
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public string? Employees { get; set; }
    public string? Department { get; set; }
    public string? Level { get; set; }
    public string? Area { get; set; }
    public string? ScheduleStart { get; set; }
    public string? ScheduleEnd { get; set; }
}
