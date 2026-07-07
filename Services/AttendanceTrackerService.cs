using AttendanceApp.Data;
using AttendanceApp.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceApp.Services;

public class AttendanceTrackerService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    private static readonly Dictionary<string, string> StatusAr = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Present", "حاضر" },
        { "Late", "متأخر" },
        { "Early Leave", "انصراف مبكر" },
        { "Absent", "غائب" },
        { "Leave", "إجازة" },
        { "Work From Home", "عمل من المنزل" },
        { "Weekly Rest", "راحة أسبوعية" },
        { "Holiday", "عطلة" },
        { "Mission", "مأمورية" },
        { "Training", "دورة تدريب" },
    };

    public AttendanceTrackerService(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<List<DailyAttendance>> GetDailyAttendanceAsync(string financialNo, DateTime date)
    {
        using var db = await _factory.CreateDbContextAsync();

        return await db.DailyAttendances
            .Include(d => d.LeaveType)
            .Include(d => d.Employee)
            .Where(d => d.EmployeeFinancialNo == financialNo && d.Date == date)
            .ToListAsync();
    }

    public async Task<List<DailyAttendance>> GetEmployeeAttendanceAsync(string financialNo, DateTime fromDate, DateTime toDate)
    {
        using var db = await _factory.CreateDbContextAsync();

        return await db.DailyAttendances
            .Include(d => d.LeaveType)
            .Include(d => d.Employee)
            .Where(d => d.EmployeeFinancialNo == financialNo && d.Date >= fromDate && d.Date <= toDate)
            .OrderBy(d => d.Date)
            .ToListAsync();
    }

    public async Task<List<DailyAttendance>> GetAllAttendanceAsync(DateTime date)
    {
        using var db = await _factory.CreateDbContextAsync();

        return await db.DailyAttendances
            .Include(d => d.LeaveType)
            .Include(d => d.Employee)
            .Where(d => d.Date == date)
            .OrderBy(d => d.Employee!.Name)
            .ToListAsync();
    }

    public async Task<List<DailyAttendance>> QueryAttendanceAsync(List<string>? employeeNos, DateTime fromDate, DateTime toDate)
    {
        using var db = await _factory.CreateDbContextAsync();

        var query = db.DailyAttendances
            .Include(d => d.LeaveType)
            .Include(d => d.Employee)
            .Where(d => d.Date >= fromDate && d.Date <= toDate);

        if (employeeNos != null && employeeNos.Count > 0)
            query = query.Where(d => employeeNos.Contains(d.EmployeeFinancialNo));

        return await query
            .OrderBy(d => d.Employee!.Name)
            .ThenBy(d => d.Date)
            .ToListAsync();
    }

    public async Task<List<LeaveBalance>> GetLeaveBalancesAsync(string? financialNo)
    {
        using var db = await _factory.CreateDbContextAsync();

        var query = db.LeaveBalances
            .Include(b => b.Employee)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(financialNo))
            query = query.Where(b => b.EmployeeFinancialNo == financialNo);

        return await query
            .OrderBy(b => b.EmployeeFinancialNo)
            .ThenByDescending(b => b.Year)
            .ToListAsync();
    }

    public async Task<string> BulkUpdateStatusAsync(DateTime date, List<BulkStatusUpdate> updates)
    {
        using var db = await _factory.CreateDbContextAsync();
        var updated = 0;

        foreach (var u in updates)
        {
            var record = await db.DailyAttendances
                .FirstOrDefaultAsync(d => d.EmployeeFinancialNo == u.FinancialNo && d.Date == date);

            if (record == null)
            {
                db.DailyAttendances.Add(new DailyAttendance
                {
                    EmployeeFinancialNo = u.FinancialNo,
                    Date = date,
                    Status = u.Status,
                    LeaveTypeId = u.LeaveTypeId
                });
            }
            else
            {
                record.Status = u.Status;
                record.LeaveTypeId = u.LeaveTypeId;
            }

            updated++;
        }

        await db.SaveChangesAsync();
        return $"Updated {updated} records";
    }

    public static string GetStatusArabic(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return "-";
        return StatusAr.TryGetValue(status, out var ar) ? ar : status;
    }
}

public class BulkStatusUpdate
{
    public string FinancialNo { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? LeaveTypeId { get; set; }
}
