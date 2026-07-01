using AttendanceApp.Services;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExportController : ControllerBase
{
    private readonly ExportService _exportService;
    private static readonly string[] MonthNames = { "", "يناير", "فبراير", "مارس", "ابريل", "مايو", "يونيو", "يوليو", "اغسطس", "سبتمبر", "اكتوبر", "نوفمبر", "ديسمبر" };

    public ExportController(ExportService exportService)
    {
        _exportService = exportService;
    }

    [HttpGet("departments")]
    public async Task<IActionResult> GetDepartments()
    {
        try
        {
            var depts = await _exportService.GetDepartmentsAsync();
            return Ok(depts);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("preview")]
    public async Task<IActionResult> Preview([FromQuery] int year, [FromQuery] int month, [FromQuery] string? department)
    {
        try
        {
            var data = await _exportService.GetPreviewDataAsync(year, month, department);
            return Ok(data);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("monthly")]
    public async Task<IActionResult> ExportMonthly([FromQuery] int year, [FromQuery] int month, [FromQuery] string? department)
    {
        try
        {
            var data = await _exportService.GenerateMonthlySheetAsync(year, month, department);
            return File(data, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                BuildFileName(year, month, department, "xlsx"));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("monthly/pdf")]
    public async Task<IActionResult> ExportMonthlyPdf([FromQuery] int year, [FromQuery] int month, [FromQuery] string? department)
    {
        try
        {
            var data = await _exportService.GenerateMonthlyPdfAsync(year, month, department);
            return File(data, "application/pdf",
                BuildFileName(year, month, department, "pdf"));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private static string BuildFileName(int year, int month, string? department, string extension)
    {
        var monthName = month >= 1 && month <= 12 ? MonthNames[month] : month.ToString("D2");
        var dept = string.IsNullOrWhiteSpace(department) ? "كل_الإدارات" : department.Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars())
            dept = dept.Replace(invalid, '_');

        return $"{monthName}_{year}_{dept}.{extension}";
    }
}
