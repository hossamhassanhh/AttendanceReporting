using Microsoft.AspNetCore.Mvc;
using AttendanceApp.Models;
using AttendanceApp.Services;

namespace AttendanceApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AttendanceController : ControllerBase
{
    private readonly AttendanceService _attendanceService;

    public AttendanceController(AttendanceService attendanceService)
    {
        _attendanceService = attendanceService;
    }

    [HttpPost("query")]
    public async Task<IActionResult> QueryAttendance([FromBody] AttendanceRequest request)
    {
        try
        {
            if (request.EmployeeCodes == null || request.EmployeeCodes.Count == 0)
                return BadRequest(new { error = "At least one employee code is required." });

            if (string.IsNullOrWhiteSpace(request.StartDate) || string.IsNullOrWhiteSpace(request.EndDate))
                return BadRequest(new { error = "Start date and end date are required." });

            var results = await _attendanceService.GetAttendanceAsync(request.EmployeeCodes, request.StartDate, request.EndDate);
            return Ok(results);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message, details = ex.ToString() });
        }
    }

    [HttpGet("{employeeCode}")]
    public async Task<IActionResult> GetAttendance(int employeeCode, [FromQuery] string date = "")
    {
        try
        {
            if (string.IsNullOrWhiteSpace(date))
                date = DateTime.Now.ToString("yyyy-MM-dd");

            var endDate = DateTime.Parse(date).AddDays(1).ToString("yyyy-MM-dd");

            var request = new AttendanceRequest
            {
                EmployeeCodes = new List<int> { employeeCode },
                StartDate = date + " 00:00:00.000 +0200",
                EndDate = endDate + " 00:00:00.000 +0200"
            };

            var results = await _attendanceService.GetAttendanceAsync(request.EmployeeCodes, request.StartDate, request.EndDate);
            return Ok(results.FirstOrDefault());
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message, details = ex.ToString() });
        }
    }
}
