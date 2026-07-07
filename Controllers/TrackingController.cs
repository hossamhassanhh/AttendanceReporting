using AttendanceApp.Services;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AttendanceApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TrackingController : ControllerBase
{
    private readonly AttendanceTrackerService _tracker;

    public TrackingController(AttendanceTrackerService tracker)
    {
        _tracker = tracker;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    [HttpGet("{financialNo}")]
    public async Task<IActionResult> GetAttendance(string financialNo, [FromQuery] string? date)
    {
        var d = DateTime.TryParse(date, out var dt) ? dt : DateTime.Today;
        var results = await _tracker.GetDailyAttendanceAsync(financialNo, d);
        return Ok(results);
    }

    [HttpGet("{financialNo}/range")]
    public async Task<IActionResult> GetAttendanceRange(string financialNo, [FromQuery] string? from, [FromQuery] string? to)
    {
        var fromDate = DateTime.TryParse(from, out var fd) ? fd : DateTime.Today.AddDays(-30);
        var toDate = DateTime.TryParse(to, out var td) ? td : DateTime.Today;
        var results = await _tracker.GetEmployeeAttendanceAsync(financialNo, fromDate, toDate);
        return Ok(results);
    }

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

    [HttpGet("query")]
    public async Task<IActionResult> Query([FromQuery] string? employees, [FromQuery] string? from, [FromQuery] string? to)
    {
        var fromDate = DateTime.TryParse(from, out var fd) ? fd : DateTime.Today;
        var toDate = DateTime.TryParse(to, out var td) ? td : DateTime.Today;

        List<string>? empList = null;
        if (!string.IsNullOrWhiteSpace(employees))
            empList = employees.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        var results = await _tracker.QueryAttendanceAsync(empList, fromDate, toDate);

        var mapped = results.Select(r => new
        {
            r.Id,
            r.EmployeeFinancialNo,
            EmployeeName = r.Employee?.Name ?? "",
            r.Date,
            DateDisplay = ToEgyptTime(r.Date)!.Value.ToString("yyyy-MM-dd"),
            FirstPunch = ToEgyptTime(r.FirstPunch),
            LastPunch = ToEgyptTime(r.LastPunch),
            r.ScheduledStart,
            r.ScheduledEnd,
            r.Status,
            StatusAr = AttendanceTrackerService.GetStatusArabic(r.Status)
        }).ToList();

        return Ok(mapped);
    }

    [HttpGet("export/excel")]
    public async Task<IActionResult> ExportExcel([FromQuery] string? employees, [FromQuery] string? from, [FromQuery] string? to)
    {
        var fromDate = DateTime.TryParse(from, out var fd) ? fd : DateTime.Today;
        var toDate = DateTime.TryParse(to, out var td) ? td : DateTime.Today;

        List<string>? empList = null;
        if (!string.IsNullOrWhiteSpace(employees))
            empList = employees.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        var results = await _tracker.QueryAttendanceAsync(empList, fromDate, toDate);
        var egyptZone = TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time");

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Attendance");

        ws.Cell(1, 1).Value = "تقرير الحضور";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(2, 1).Value = $"من {fromDate:yyyy-MM-dd} إلى {toDate:yyyy-MM-dd}";

        ws.Cell(4, 1).Value = "الرقم المالي";
        ws.Cell(4, 2).Value = "الاسم";
        ws.Cell(4, 3).Value = "التاريخ";
        ws.Cell(4, 4).Value = "الحالة";
        ws.Cell(4, 5).Value = "الحضور";
        ws.Cell(4, 6).Value = "الانصراف";
        ws.Cell(4, 7).Value = "الموعد";

        var headerRange = ws.Range(4, 1, 4, 7);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(0xD9D9D9);
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        int row = 5;
        foreach (var r in results)
        {
            ws.Cell(row, 1).Value = r.EmployeeFinancialNo;
            ws.Cell(row, 2).Value = r.Employee?.Name ?? "";
            ws.Cell(row, 3).Value = ToEgyptTime(r.Date)!.Value.ToString("yyyy-MM-dd");
            ws.Cell(row, 4).Value = AttendanceTrackerService.GetStatusArabic(r.Status);
            ws.Cell(row, 5).Value = r.FirstPunch.HasValue ? ToEgyptTime(r.FirstPunch)!.Value.ToString("HH:mm:ss") : "-";
            ws.Cell(row, 6).Value = r.LastPunch.HasValue ? ToEgyptTime(r.LastPunch)!.Value.ToString("HH:mm:ss") : "-";
            ws.Cell(row, 7).Value = $"{r.ScheduledStart ?? "-"} - {r.ScheduledEnd ?? "-"}";

            ws.Range(row, 1, row, 7).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(row, 1, row, 7).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            row++;
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"attendance_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.xlsx");
    }

    [HttpGet("export/pdf")]
    public async Task<IActionResult> ExportPdf([FromQuery] string? employees, [FromQuery] string? from, [FromQuery] string? to)
    {
        var fromDate = DateTime.TryParse(from, out var fd) ? fd : DateTime.Today;
        var toDate = DateTime.TryParse(to, out var td) ? td : DateTime.Today;

        List<string>? empList = null;
        if (!string.IsNullOrWhiteSpace(employees))
            empList = employees.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        var results = await _tracker.QueryAttendanceAsync(empList, fromDate, toDate);
        var egyptZone = TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time");

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(20);
                page.Size(PageSizes.A4.Landscape());

                page.Header().Text("تقرير الحضور").AlignRight().FontSize(16).Bold();
                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(50);
                        c.RelativeColumn(2);
                        c.ConstantColumn(80);
                        c.ConstantColumn(70);
                        c.ConstantColumn(70);
                        c.ConstantColumn(70);
                        c.ConstantColumn(90);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Text("الرقم المالي").Bold().FontSize(9);
                        header.Cell().Text("الاسم").Bold().FontSize(9);
                        header.Cell().Text("التاريخ").Bold().FontSize(9);
                        header.Cell().Text("الحالة").Bold().FontSize(9);
                        header.Cell().Text("الحضور").Bold().FontSize(9);
                        header.Cell().Text("الانصراف").Bold().FontSize(9);
                        header.Cell().Text("الموعد").Bold().FontSize(9);
                    });

                    foreach (var r in results)
                    {
                        var dateStr = ToEgyptTime(r.Date)!.Value.ToString("yyyy-MM-dd");
                        var firstStr = r.FirstPunch.HasValue ? ToEgyptTime(r.FirstPunch)!.Value.ToString("HH:mm:ss") : "-";
                        var lastStr = r.LastPunch.HasValue ? ToEgyptTime(r.LastPunch)!.Value.ToString("HH:mm:ss") : "-";
                        var schedStr = $"{r.ScheduledStart ?? "-"} - {r.ScheduledEnd ?? "-"}";

                        table.Cell().Text(r.EmployeeFinancialNo).FontSize(8);
                        table.Cell().Text(r.Employee?.Name ?? "").FontSize(8);
                        table.Cell().Text(dateStr).FontSize(8);
                        table.Cell().Text(AttendanceTrackerService.GetStatusArabic(r.Status)).FontSize(8);
                        table.Cell().Text(firstStr).FontSize(8);
                        table.Cell().Text(lastStr).FontSize(8);
                        table.Cell().Text(schedStr).FontSize(8);
                    }
                });
            });
        });

        using var ms = new MemoryStream();
        doc.GeneratePdf(ms);
        return File(ms.ToArray(), "application/pdf", $"attendance_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.pdf");
    }

    [HttpGet("{financialNo}/balances")]
    public async Task<IActionResult> GetBalances(string financialNo)
    {
        var balances = await _tracker.GetLeaveBalancesAsync(financialNo);
        return Ok(balances);
    }

    [HttpGet("balances")]
    public async Task<IActionResult> GetAllBalances([FromQuery] string? financialNo)
    {
        var balances = await _tracker.GetLeaveBalancesAsync(financialNo);
        return Ok(balances);
    }

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
}

public class BulkUpdateRequest
{
    public DateTime Date { get; set; }
    public List<BulkStatusUpdate> Updates { get; set; } = new();
}
