using AttendanceApp.Data;
using AttendanceApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AttendanceApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmployeesController : ControllerBase
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public EmployeesController(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search)
    {
        using var db = await _factory.CreateDbContextAsync();
        var query = db.Employees.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(e => e.Name.Contains(search) || e.FinancialNo.Contains(search));
        }

        var employees = await query.OrderBy(e => e.Name).ToListAsync();
        return Ok(employees);
    }

    [HttpGet("{financialNo}")]
    public async Task<IActionResult> Get(string financialNo)
    {
        using var db = await _factory.CreateDbContextAsync();
        var emp = await db.Employees.FindAsync(financialNo);
        if (emp == null) return NotFound();
        return Ok(emp);
    }

    [HttpGet("schedule-rules")]
    public async Task<IActionResult> GetScheduleRules()
    {
        using var db = await _factory.CreateDbContextAsync();
        var rules = await db.ScheduleRules.Where(r => r.IsActive).ToListAsync();
        return Ok(rules);
    }
}
