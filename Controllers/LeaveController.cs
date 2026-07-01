using AttendanceApp.Services;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceApp.Controllers;

[ApiController]
[Route("api/[controller]")]
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
    public async Task<IActionResult> DownloadUploadTemplate()
    {
        var data = await _leaveService.GenerateLeaveUploadTemplateAsync();
        return File(data, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "leave-upload-template.xlsx");
    }

    [HttpPost("grant")]
    public async Task<IActionResult> GrantLeave([FromBody] GrantLeaveRequest request)
    {
        try
        {
            var result = await _leaveService.GrantLeaveAsync(
                request.FinancialNo,
                request.LeaveTypeId,
                request.FromDate,
                request.ToDate,
                request.DaysCount,
                request.Reason
            );
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{financialNo}/transactions")]
    public async Task<IActionResult> GetTransactions(string financialNo)
    {
        var transactions = await _leaveService.GetLeaveTransactionsAsync(financialNo);
        return Ok(transactions);
    }

    [HttpGet("transactions")]
    public async Task<IActionResult> GetAllTransactions([FromQuery] string? financialNo)
    {
        var transactions = await _leaveService.GetLeaveTransactionsAsync(financialNo);
        return Ok(transactions);
    }
}

public class GrantLeaveRequest
{
    public string FinancialNo { get; set; } = string.Empty;
    public int LeaveTypeId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public double DaysCount { get; set; }
    public string? Reason { get; set; }
}
