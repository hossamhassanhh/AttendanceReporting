using AttendanceApp.Data;
using AttendanceApp.Models;
using AttendanceApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace AttendanceApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "Employees")]
public class EmployeesController : ControllerBase
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly TabularReportExportService _reportExporter;

    public EmployeesController(
        IDbContextFactory<AppDbContext> factory,
        TabularReportExportService reportExporter)
    {
        _factory = factory;
        _reportExporter = reportExporter;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? department,
        [FromQuery] string? level,
        [FromQuery] string? area,
        [FromQuery] string? status)
    {
        var employees = await QueryEmployeesAsync(search, department, level, area, status);
        return Ok(employees);
    }

    [Authorize(Policy = "Exports")]
    [HttpGet("export/{format}")]
    public async Task<IActionResult> Export(
        string format,
        [FromQuery] string? search,
        [FromQuery] string? department,
        [FromQuery] string? level,
        [FromQuery] string? area,
        [FromQuery] string? status,
        [FromQuery] string? lang)
    {
        if (!string.Equals(format, "excel", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Supported formats are excel and pdf." });

        var isArabic = !string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase);
        var employees = await QueryEmployeesAsync(search, department, level, area, status);
        var columns = isArabic
            ? new[] { "الرقم المالي", "الاسم", "المسمى الوظيفي", "المستوى", "الإدارة", "منطقة العمل", "الحالة", "موعد العمل" }
            : new[] { "Financial No", "Name", "Job Title", "Level", "Department", "Work Area", "Status", "Schedule" };
        var rows = employees.Select(employee => (IReadOnlyList<object?>)new object?[]
        {
            employee.FinancialNo,
            employee.Name,
            employee.JobTitle,
            employee.Level,
            employee.Department,
            employee.WorkLocation,
            employee.JobStatus,
            string.IsNullOrWhiteSpace(employee.ScheduleStart) || string.IsNullOrWhiteSpace(employee.ScheduleEnd)
                ? "-"
                : $"{employee.ScheduleStart} - {employee.ScheduleEnd}"
        }).ToList();
        var title = isArabic ? "تقرير الموظفين" : "Employees Report";
        var fileStem = isArabic ? "تقرير_الموظفين" : "employees_report";
        var data = string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase)
            ? _reportExporter.GeneratePdf(title, null, columns, rows, isArabic)
            : _reportExporter.GenerateExcel(title, null, isArabic ? "الموظفون" : "Employees", columns, rows, isArabic);
        var extension = string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase) ? "pdf" : "xlsx";
        var contentType = extension == "pdf"
            ? "application/pdf"
            : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        return File(data, contentType, $"{fileStem}_{DateTime.Today:yyyyMMdd}.{extension}");
    }

    [HttpGet("filter-options")]
    public async Task<IActionResult> GetFilterOptions()
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

        var statuses = await db.Employees.AsNoTracking()
            .Where(e => e.JobStatus != null && e.JobStatus != "")
            .Select(e => e.JobStatus!)
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync();

        return Ok(new { Departments = departments, Levels = levels, Areas = areas, Statuses = statuses });
    }

    [HttpGet("{financialNo}")]
    public async Task<IActionResult> Get(string financialNo)
    {
        using var db = await _factory.CreateDbContextAsync();
        var emp = await db.Employees.AsNoTracking()
            .FirstOrDefaultAsync(item => item.FinancialNo == financialNo);
        if (emp == null) return NotFound();
        return Ok(emp);
    }

    [HttpGet("schedule-rules")]
    public async Task<IActionResult> GetScheduleRules()
    {
        using var db = await _factory.CreateDbContextAsync();
        var rules = await db.ScheduleRules.AsNoTracking().Where(r => r.IsActive).ToListAsync();
        return Ok(rules);
    }

    private async Task<List<Employee>> QueryEmployeesAsync(
        string? search,
        string? department,
        string? level,
        string? area,
        string? status)
    {
        using var db = await _factory.CreateDbContextAsync();
        var query = db.Employees.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(employee => employee.Name.Contains(search) || employee.FinancialNo.Contains(search));
        if (!string.IsNullOrWhiteSpace(department))
            query = query.Where(employee => employee.Department == department);
        if (!string.IsNullOrWhiteSpace(level))
            query = AttendanceStatusRules.IsStandardJobLevelsGroup(level)
                ? query.Where(employee => employee.Level != null && AttendanceStatusRules.StandardJobLevels.Contains(employee.Level))
                : query.Where(employee => employee.Level == level);
        if (!string.IsNullOrWhiteSpace(area))
            query = query.Where(employee => employee.WorkLocation == area);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(employee => employee.JobStatus == status);

        var employees = await query.ToListAsync();
        return employees
            .OrderBy(employee => long.TryParse(employee.FinancialNo, out var financialNo) ? financialNo : long.MaxValue)
            .ThenBy(employee => employee.FinancialNo)
            .ToList();
    }
}
