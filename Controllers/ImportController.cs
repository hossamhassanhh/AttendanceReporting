using AttendanceApp.Data;
using AttendanceApp.Models;
using AttendanceApp.Services;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AttendanceApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "Imports")]
public class ImportController : ControllerBase
{
    private readonly ExcelImportService _importService;
    private readonly ZkAttendanceService _zkService;
    private readonly LeaveFillService _leaveFillService;
    private readonly LeaveService _leaveService;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public ImportController(ExcelImportService importService, ZkAttendanceService zkService, LeaveFillService leaveFillService, LeaveService leaveService, IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _importService = importService;
        _zkService = zkService;
        _leaveFillService = leaveFillService;
        _leaveService = leaveService;
        _dbContextFactory = dbContextFactory;
    }

    [HttpPost("all")]
    public async Task<IActionResult> ImportAll()
    {
        try
        {
            var result = await _importService.ImportAllAsync();
            return Ok(new { message = result });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("zk-attendance")]
    public async Task<IActionResult> PopulateZkAttendance([FromQuery] string? date, [FromQuery] string? from, [FromQuery] string? to)
    {
        try
        {
            if (!string.IsNullOrEmpty(from) && !string.IsNullOrEmpty(to)
                && DateTime.TryParse(from, out var fromDate) && DateTime.TryParse(to, out var toDate))
            {
                var result = await _zkService.PopulateDailyAttendanceRangeAsync(fromDate, toDate);
                return Ok(new { message = result });
            }

            var d = DateTime.TryParse(date, out var dt) ? dt : DateTime.Today;
            var single = await _zkService.PopulateDailyAttendanceRangeAsync(d, d);
            return Ok(new { message = single });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("employees")]
    public async Task<IActionResult> ImportEmployees(IFormFile file)
    {
        return await ImportUploadedFileAsync(file, path => _importService.ImportEmployeesLevelsAsync(path));
    }

    [HttpPost("balances")]
    public async Task<IActionResult> ImportBalances(IFormFile file)
    {
        return await ImportUploadedFileAsync(file, path => _importService.ImportLeaveBalancesAsync(path));
    }

    [HttpPost("monthly-attendance")]
    public async Task<IActionResult> ImportMonthlyAttendance(IFormFile file)
    {
        return await ImportUploadedFileAsync(file, path => _importService.ImportMonthlyAttendanceAsync(path));
    }

    [HttpPost("leaves")]
    public async Task<IActionResult> ImportLeaves(IFormFile file)
    {
        var enteredBy = User.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrWhiteSpace(enteredBy))
            return Unauthorized(new { error = "Authenticated username is unavailable" });

        return await ImportUploadedFileAsync(file, path => _leaveService.ImportLeaveUploadAsync(path, enteredBy));
    }

    [HttpPost("fill-leaves")]
    public async Task<IActionResult> FillLeaves([FromQuery] int? year, [FromQuery] int? month)
    {
        try
        {
            var y = year ?? 2026;
            var m = month ?? 5;
            var result = await _leaveFillService.FillLeavesFromMonthlyAsync(y, m);
            return Ok(new { message = result });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("sync-status")]
    public async Task<IActionResult> GetSyncStatus()
    {
        using var db = await _dbContextFactory.CreateDbContextAsync();
        var state = await db.SyncStates.FirstOrDefaultAsync(s => s.Key == "ZkAttendance");
        var lastSyncTime = state?.LastSyncTime;
        var isRunning = lastSyncTime.HasValue && DateTime.Now - lastSyncTime.Value <= TimeSpan.FromMinutes(2);

        return Ok(new
        {
            lastSyncTime,
            lastProcessedId = state?.LastProcessedTransactionId ?? 0,
            isRunning
        });
    }

    [HttpPost("zk-backfill")]
    public async Task<IActionResult> BackfillAll()
    {
        try
        {
            var result = await _zkService.BackfillAllAsync();

            using var db = await _dbContextFactory.CreateDbContextAsync();
            var state = await db.SyncStates.FirstOrDefaultAsync(s => s.Key == "ZkAttendance");
            if (state == null)
            {
                state = new SyncState { Key = "ZkAttendance" };
                db.SyncStates.Add(state);
            }
            state.LastProcessedTransactionId = result.MaxId;
            state.LastSyncTime = DateTime.Now;
            await db.SaveChangesAsync();

            return Ok(new { message = $"Backfill complete: {result.ProcessedCount} records processed, lastId={result.MaxId}" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("template/employees")]
    public IActionResult DownloadEmployeesTemplate([FromQuery] string? lang)
    {
        var isArabic = !string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase);
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(isArabic ? "الموظفون" : "Employees");
        ws.RightToLeft = isArabic;
        var headers = isArabic
            ? new[] { "الرقم المالي", "الاسم", "المسمى الوظيفي", "النوع", "الإدارة", "المستوى" }
            : new[] { "Financial No", "Name", "Job Title", "Gender", "Department", "Level" };
        SetTemplateHeaders(ws, headers);
        AddSampleSheet(
            wb,
            isArabic,
            headers,
            isArabic
                ? new object[] { "1001", "اسم الموظف", "محاسب", "ذكر", "المركز الرئيسي", "Level 2" }
                : new object[] { "1001", "Employee Name", "Accountant", "Male", "Head Office", "Level 2" });
        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        var fileName = isArabic ? "قالب-الموظفين.xlsx" : "employees-template.xlsx";
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpGet("template/balances")]
    public IActionResult DownloadBalancesTemplate([FromQuery] string? lang)
    {
        var isArabic = !string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase);
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(isArabic ? "الأرصدة" : "Balances");
        ws.RightToLeft = isArabic;
        var headers = isArabic
            ? new[] { "الرقم المالي", "الاسم", "الإجازة الاعتيادية", "الإجازة العارضة", "رصيد الراحة", "رصيد العطلات", "موقع العمل", "الحالة الوظيفية" }
            : new[] { "Financial No", "Name", "Regular Leave", "Casual Leave", "Rest Allowance", "Holiday Allowance", "Work Location", "Job Status" };
        SetTemplateHeaders(ws, headers);
        AddSampleSheet(
            wb,
            isArabic,
            headers,
            isArabic
                ? new object[] { "1001", "اسم الموظف", 21, 7, 3, 2, "المركز الرئيسي", "نشط" }
                : new object[] { "1001", "Employee Name", 21, 7, 3, 2, "Head Office", "Active" });
        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        var fileName = isArabic ? "قالب-الأرصدة.xlsx" : "balances-template.xlsx";
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpGet("template/monthly")]
    public IActionResult DownloadMonthlyTemplate([FromQuery] string? lang)
    {
        var isArabic = !string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase);
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(isArabic ? "الحضور الشهري" : "Monthly Attendance");
        ws.RightToLeft = isArabic;

        ws.Cell(1, 1).Value = isArabic ? "فترة كشف الحضور" : "Time Sheet Period";
        ws.Range(1, 1, 1, 48).Merge();
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        ws.Cell(2, 1).Value = isArabic ? "الإدارة:" : "Department:";
        ws.Cell(2, 2).Value = isArabic ? "اسم الإدارة" : "Department Name";
        ws.Range(2, 2, 2, 5).Merge();

        ws.Cell(4, 1).Value = isArabic ? "الرقم المالي" : "Financial No";
        ws.Cell(4, 2).Value = isArabic ? "الاسم" : "Name";
        ws.Cell(4, 3).Value = isArabic ? "المسمى الوظيفي" : "Job Title";
        for (var d = 1; d <= 31; d++)
            ws.Cell(4, 3 + d).Value = d.ToString();
        ws.Cell(4, 35).Value = isArabic ? "إجمالي أيام العمل" : "Total Working Days";
        ws.Cell(4, 36).Value = isArabic ? "توقيع الموظف" : "Employee Signature";
        ws.Range(4, 1, 4, 48).Style.Font.Bold = true;
        ws.Range(4, 1, 4, 48).Style.Fill.BackgroundColor = XLColor.FromHtml("#D9E2F3");
        ws.Range(4, 1, 4, 48).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        ws.Range(4, 1, 4, 48).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        ws.Range(4, 1, 4, 48).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        ws.Columns().AdjustToContents();

        var sampleHeaders = Enumerable.Range(1, 36)
            .Select(column => ws.Cell(4, column).GetString())
            .ToArray();
        var sampleValues = new object[36];
        sampleValues[0] = "1001";
        sampleValues[1] = isArabic ? "اسم الموظف" : "Employee Name";
        sampleValues[2] = isArabic ? "المسمى الوظيفي" : "Job Title";
        for (var day = 1; day <= 31; day++)
            sampleValues[2 + day] = day <= 28 ? "X" : "";
        sampleValues[34] = 28;
        AddSampleSheet(wb, isArabic, sampleHeaders, sampleValues);

        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        var fileName = isArabic ? "قالب-الحضور-الشهري.xlsx" : "monthly-attendance-template.xlsx";
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    private static void SetTemplateHeaders(IXLWorksheet sheet, IReadOnlyList<string> headers)
    {
        for (var column = 1; column <= headers.Count; column++)
            sheet.Cell(1, column).Value = headers[column - 1];

        var range = sheet.Range(1, 1, 1, headers.Count);
        range.Style.Font.Bold = true;
        range.Style.Font.FontColor = XLColor.White;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#1A365D");
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        sheet.SheetView.FreezeRows(1);
    }

    private static void AddSampleSheet(
        XLWorkbook workbook,
        bool isArabic,
        IReadOnlyList<string> headers,
        IReadOnlyList<object> values)
    {
        var sample = workbook.Worksheets.Add(isArabic ? "مثال" : "Sample");
        sample.RightToLeft = isArabic;
        sample.Cell(1, 1).Value = isArabic
            ? "للاسترشاد فقط - أدخل البيانات في الورقة الأولى"
            : "For guidance only - enter data in the first worksheet";
        sample.Range(1, 1, 1, headers.Count).Merge();
        sample.Cell(1, 1).Style.Font.Bold = true;
        sample.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF2CC");

        for (var column = 1; column <= headers.Count; column++)
        {
            sample.Cell(2, column).Value = headers[column - 1];
            sample.Cell(3, column).Value = XLCellValue.FromObject(values[column - 1]);
        }

        var header = sample.Range(2, 1, 2, headers.Count);
        header.Style.Font.Bold = true;
        header.Style.Font.FontColor = XLColor.White;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#1A365D");
        sample.Columns().AdjustToContents();
        sample.SheetView.FreezeRows(2);
    }

    private static async Task<IActionResult> ImportUploadedFileAsync(IFormFile? file, Func<string, Task<string>> importer)
    {
        if (file == null || file.Length == 0)
            return new BadRequestObjectResult(new { error = "Upload file is required." });

        var tempPath = Path.Combine(Path.GetTempPath(), $"attendance-import-{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}");
        try
        {
            await using (var stream = System.IO.File.Create(tempPath))
                await file.CopyToAsync(stream);

            var result = await importer(tempPath);
            return new OkObjectResult(new { message = result });
        }
        catch (InvalidDataException ex)
        {
            return new BadRequestObjectResult(new { error = ex.Message });
        }
        catch (FormatException ex)
        {
            return new BadRequestObjectResult(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { error = ex.Message }) { StatusCode = 500 };
        }
        finally
        {
            try { System.IO.File.Delete(tempPath); } catch { }
        }
    }
}
