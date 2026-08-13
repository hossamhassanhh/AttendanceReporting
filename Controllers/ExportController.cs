using AttendanceApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "MonthlyReports")]
public class ExportController : ControllerBase
{
    private readonly ExportService _exportService;

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

    [HttpGet("filter-options")]
    public async Task<IActionResult> GetFilterOptions()
    {
        try
        {
            var options = await _exportService.GetFilterOptionsAsync();
            return Ok(options);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("preview")]
    public async Task<IActionResult> Preview(
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] string? department,
        [FromQuery] string? level,
        [FromQuery] string? area)
    {
        try
        {
            var data = await _exportService.GetPreviewDataAsync(year, month, department, level, area);
            return Ok(data);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("monthly")]
    public async Task<IActionResult> ExportMonthly(
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] string? department,
        [FromQuery] string? level,
        [FromQuery] string? area)
    {
        try
        {
            var data = await _exportService.GenerateMonthlySheetAsync(year, month, department, level, area);
            return File(data, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                BuildFileName(year, month, department, "xlsx"));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("monthly/pdf")]
    public async Task<IActionResult> ExportMonthlyPdf(
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] string? department,
        [FromQuery] string? level,
        [FromQuery] string? area,
        [FromQuery] string? lang)
    {
        try
        {
            var data = await _exportService.GenerateMonthlyPdfAsync(year, month, department, level, area, lang);
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
        var period = month >= 1 && month <= 12 ? $"{year}-{month:D2}" : year.ToString();
        var dept = string.IsNullOrWhiteSpace(department) ? "All_Departments" : department.Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars())
            dept = dept.Replace(invalid, '_');

        return $"Monthly_Report_{period}_{dept}.{extension}";
    }
}
