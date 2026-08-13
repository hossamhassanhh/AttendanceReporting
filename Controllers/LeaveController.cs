using AttendanceApp.Services;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Security.Claims;

namespace AttendanceApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "Leaves")]
public class LeaveController : ControllerBase
{
    private readonly LeaveService _leaveService;

    public LeaveController(LeaveService leaveService)
    {
        _leaveService = leaveService;
    }

    [HttpGet("types")]
    public async Task<IActionResult> GetLeaveTypes()
    {
        var types = await _leaveService.GetLeaveTypesAsync();
        return Ok(types);
    }

    [HttpGet("upload-template")]
    public async Task<IActionResult> DownloadUploadTemplate([FromQuery] string? lang)
    {
        var isArabic = !string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase);
        var data = await _leaveService.GenerateLeaveUploadTemplateAsync(isArabic);
        var fileName = isArabic ? "قالب-استيراد-الإجازات.xlsx" : "leave-upload-template.xlsx";
        return File(data, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpPost("grant")]
    public async Task<IActionResult> GrantLeave([FromBody] GrantLeaveRequest request)
    {
        try
        {
            var enteredBy = User.FindFirstValue(ClaimTypes.Name)
                ?? throw new UnauthorizedAccessException("Authenticated username is unavailable");
            var result = await _leaveService.GrantLeaveAsync(
                request.FinancialNo,
                request.LeaveTypeId,
                request.FromDate,
                request.ToDate,
                request.Reason,
                enteredBy
            );
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("transactions/{id:int}")]
    public async Task<IActionResult> UpdateTransaction(int id, [FromBody] UpdateLeaveRequest request)
    {
        try
        {
            var result = await _leaveService.UpdateLeaveAsync(
                id,
                request.LeaveTypeId,
                request.FromDate,
                request.ToDate,
                request.Reason);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("transactions/{id:int}")]
    public async Task<IActionResult> DeleteTransaction(int id)
    {
        try
        {
            await _leaveService.DeleteLeaveAsync(id);
            return Ok(new { message = "Leave transaction deleted" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("day-count")]
    public async Task<IActionResult> GetDayCount([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
    {
        var days = await _leaveService.CalculateActualLeaveDaysAsync(fromDate, toDate);
        return Ok(new { daysCount = days });
    }

    [HttpGet("{financialNo}/transactions")]
    public async Task<IActionResult> GetTransactions(string financialNo)
    {
        var transactions = await _leaveService.GetLeaveLogDaysAsync(financialNo);
        return Ok(transactions);
    }

    [HttpGet("transactions")]
    public async Task<IActionResult> GetAllTransactions([FromQuery] string? financialNo, [FromQuery] int? leaveTypeId, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
    {
        var transactions = await _leaveService.GetLeaveLogDaysAsync(financialNo, leaveTypeId, fromDate, toDate);
        return Ok(transactions);
    }

    [HttpGet("all-transactions")]
    public async Task<IActionResult> GetAllTransactionsAlias([FromQuery] string? financialNo, [FromQuery] int? leaveTypeId, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
    {
        var transactions = await _leaveService.GetLeaveLogDaysAsync(financialNo, leaveTypeId, fromDate, toDate);
        return Ok(transactions);
    }

    [HttpGet("export/excel")]
    public async Task<IActionResult> ExportExcel([FromQuery] string? financialNo, [FromQuery] int? leaveTypeId, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
    {
        var transactions = await _leaveService.GetLeaveLogDaysAsync(financialNo, leaveTypeId, fromDate, toDate);
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Leave Transactions");

        sheet.Cell(1, 1).Value = "#";
        sheet.Cell(1, 2).Value = "الرقم المالي";
        sheet.Cell(1, 3).Value = "الموظف";
        sheet.Cell(1, 4).Value = "النوع";
        sheet.Cell(1, 5).Value = "الفترة";
        sheet.Cell(1, 6).Value = "أيام العمل";
        sheet.Cell(1, 7).Value = "السبب";
        sheet.Cell(1, 8).Value = "مدخل الإجازة";
        sheet.Cell(1, 9).Value = "تاريخ التسجيل";

        var headerRange = sheet.Range(1, 1, 1, 9);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1a2744");
        headerRange.Style.Font.FontColor = XLColor.White;

        for (int i = 0; i < transactions.Count; i++)
        {
            var t = transactions[i];
            sheet.Cell(i + 2, 1).Value = i + 1;
            sheet.Cell(i + 2, 2).Value = t.EmployeeFinancialNo;
            sheet.Cell(i + 2, 3).Value = t.EmployeeName;
            sheet.Cell(i + 2, 4).Value = !string.IsNullOrWhiteSpace(t.LeaveTypeNameAr) ? t.LeaveTypeNameAr : t.LeaveTypeNameEn;
            sheet.Cell(i + 2, 5).Value = $"{t.FromDate:yyyy-MM-dd} - {t.ToDate:yyyy-MM-dd}";
            sheet.Cell(i + 2, 6).Value = t.DaysCount;
            sheet.Cell(i + 2, 7).Value = t.Reason ?? "-";
            sheet.Cell(i + 2, 8).Value = t.EnteredBy;
            sheet.Cell(i + 2, 9).Value = t.CreatedAt.ToString("yyyy-MM-dd HH:mm");
        }

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "leave-transactions.xlsx");
    }

    [HttpGet("export/pdf")]
    public async Task<IActionResult> ExportPdf(
        [FromQuery] string? financialNo,
        [FromQuery] int? leaveTypeId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] string? lang)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var transactions = await _leaveService.GetLeaveLogDaysAsync(financialNo, leaveTypeId, fromDate, toDate);
        var isArabic = !string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase);

        var pdfBytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                var content = page.Content();
                if (isArabic) content = content.ContentFromRightToLeft();
                content.Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        var headers = isArabic
                            ? new[] { "#", "الرقم المالي", "الموظف", "النوع", "الفترة", "أيام العمل", "السبب", "مدخل الإجازة", "التاريخ" }
                            : new[] { "#", "Financial No", "Employee", "Type", "Period", "Working Days", "Reason", "Entered By", "Created" };
                        foreach (var label in headers)
                            header.Cell().Text(label).Bold();
                    });

                    for (int i = 0; i < transactions.Count; i++)
                    {
                        var t = transactions[i];
                        table.Cell().Text((i + 1).ToString());
                        table.Cell().Text(t.EmployeeFinancialNo);
                        table.Cell().Text(t.EmployeeName);
                        table.Cell().Text(isArabic
                            ? (!string.IsNullOrWhiteSpace(t.LeaveTypeNameAr) ? t.LeaveTypeNameAr : t.LeaveTypeNameEn)
                            : (!string.IsNullOrWhiteSpace(t.LeaveTypeNameEn) ? t.LeaveTypeNameEn : t.LeaveTypeNameAr));
                        table.Cell().Text($"{t.FromDate:yyyy-MM-dd} - {t.ToDate:yyyy-MM-dd}");
                        table.Cell().Text(t.DaysCount.ToString());
                        table.Cell().Text(t.Reason ?? "-");
                        table.Cell().Text(t.EnteredBy);
                        table.Cell().Text(t.CreatedAt.ToString("yyyy-MM-dd HH:mm"));
                    }
                });
            });
        }).GeneratePdf();

        return File(pdfBytes, "application/pdf", "leave-transactions.pdf");
    }
}

public class GrantLeaveRequest
{
    public string FinancialNo { get; set; } = string.Empty;
    public int LeaveTypeId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string? Reason { get; set; }
}

public class UpdateLeaveRequest
{
    public int LeaveTypeId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string? Reason { get; set; }
}
