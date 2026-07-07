using AttendanceApp.Data;
using AttendanceApp.Models;
using AttendanceApp.Services;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AttendanceApp.Controllers;

[ApiController]
[Route("api/[controller]")]
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
        return await ImportUploadedFileAsync(file, path => _leaveService.ImportLeaveUploadAsync(path));
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
    public IActionResult GetSyncStatus()
    {
        return Ok(new
        {
            lastSyncTime = ZkSyncBackgroundService.LastSyncTime,
            lastProcessedId = ZkSyncBackgroundService.LastProcessedId,
            isRunning = ZkSyncBackgroundService.IsRunning
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
    public IActionResult DownloadEmployeesTemplate()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Employees");
        ws.Cell(1, 1).Value = "PR";
        ws.Cell(1, 2).Value = "Name";
        ws.Cell(1, 3).Value = "Job Title";
        ws.Cell(1, 4).Value = "Gender";
        ws.Cell(1, 5).Value = "Nationality";
        ws.Cell(1, 6).Value = "Department";
        ws.Range(1, 1, 1, 6).Style.Font.Bold = true;
        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "employees-template.xlsx");
    }

    [HttpGet("template/balances")]
    public IActionResult DownloadBalancesTemplate()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Balances");
        ws.Cell(1, 1).Value = "Financial No";
        ws.Cell(1, 2).Value = "Name";
        ws.Cell(1, 3).Value = "Regular Leave";
        ws.Cell(1, 4).Value = "Casual Leave";
        ws.Cell(1, 5).Value = "Rest Allowance";
        ws.Cell(1, 6).Value = "Holiday Allowance";
        ws.Cell(1, 7).Value = "Work Location";
        ws.Cell(1, 8).Value = "Job Status";
        ws.Range(1, 1, 1, 8).Style.Font.Bold = true;
        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "balances-template.xlsx");
    }

    [HttpGet("template/monthly")]
    public IActionResult DownloadMonthlyTemplate()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Monthly");
        ws.RightToLeft = true;

        ws.Cell(1, 1).Value = "time sheet period";
        ws.Range(1, 1, 1, 48).Merge();
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        ws.Cell(2, 1).Value = "Dep :";
        ws.Cell(2, 2).Value = "الإدارة";
        ws.Range(2, 2, 2, 5).Merge();

        ws.Cell(4, 1).Value = "PR";
        ws.Cell(4, 2).Value = "Name";
        ws.Cell(4, 3).Value = "Job Title";
        for (var d = 1; d <= 31; d++)
            ws.Cell(4, 3 + d).Value = d.ToString();
        ws.Cell(4, 35).Value = "Total Working Days";
        ws.Cell(4, 36).Value = "Employee Signature";
        ws.Range(4, 1, 4, 48).Style.Font.Bold = true;
        ws.Range(4, 1, 4, 48).Style.Fill.BackgroundColor = XLColor.FromHtml("#D9E2F3");
        ws.Range(4, 1, 4, 48).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        ws.Range(4, 1, 4, 48).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        ws.Range(4, 1, 4, 48).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        ws.Cell(5, 1).Value = "1364";
        ws.Cell(5, 2).Value = "اسم الموظف";
        ws.Cell(5, 3).Value = "المسمى الوظيفى";
        for (var d = 1; d <= 31; d++)
            ws.Cell(5, 3 + d).Value = d <= 28 ? "R" : "";
        ws.Range(5, 1, 5, 48).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        ws.Range(5, 1, 5, 48).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "monthly-template.xlsx");
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
