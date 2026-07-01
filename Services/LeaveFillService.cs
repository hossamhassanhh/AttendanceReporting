using AttendanceApp.Data;
using AttendanceApp.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceApp.Services;

public class LeaveFillService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly ILogger<LeaveFillService> _logger;

    private static readonly HashSet<string> LeaveCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "A", "S", "C", "E"
    };

    private static readonly Dictionary<string, string> CodeToBalanceType = new(StringComparer.OrdinalIgnoreCase)
    {
        { "A", "Regular" },
        { "C", "Casual" },
        { "E", "Rest" },
    };

    private static readonly Dictionary<string, string> CodeToStatus = new(StringComparer.OrdinalIgnoreCase)
    {
        { "R", "Present" },
        { "W", "Weekly Rest" },
        { "H", "Holiday" },
        { "A", "Leave" },
        { "S", "Leave" },
        { "C", "Leave" },
        { "B", "Absent" },
        { "E", "Leave" },
        { "DX", "Mission" },
        { "DI", "Mission" },
        { "T", "Training" },
        { "X", "Present" },
        { "X1", "Present" },
        { "X2", "Present" },
        { "X3", "Present" },
        { "X4", "Present" },
    };

    public LeaveFillService(IDbContextFactory<AppDbContext> factory, ILogger<LeaveFillService> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task<string> FillLeavesFromMonthlyAsync(int year, int month)
    {
        using var db = await _factory.CreateDbContextAsync();
        var leaveTypes = await db.LeaveTypes.ToListAsync();
        var codeToLeaveType = leaveTypes.ToDictionary(lt => lt.Code, StringComparer.OrdinalIgnoreCase);

        var records = await db.MonthlyAttendances
            .Where(r => r.Year == year && r.Month == month)
            .OrderBy(r => r.EmployeeFinancialNo).ThenBy(r => r.Day)
            .ToListAsync();

        var processed = 0;
        var skipped = 0;

        var byEmployee = records.GroupBy(r => r.EmployeeFinancialNo);

        foreach (var group in byEmployee)
        {
            var finNo = group.Key;
            var empExists = await db.Employees.AnyAsync(e => e.FinancialNo == finNo);
            if (!empExists) continue;

            var balance = await db.LeaveBalances
                .FirstOrDefaultAsync(b => b.EmployeeFinancialNo == finNo && b.Year == year);

            foreach (var rec in group)
            {
                var code = rec.Code?.Trim() ?? string.Empty;
                var status = CodeToStatus.GetValueOrDefault(code, "Unknown");
                var date = new DateTime(year, month, rec.Day);

                var existingAtt = await db.DailyAttendances
                    .FirstOrDefaultAsync(a => a.EmployeeFinancialNo == finNo && a.Date == date);

                if (existingAtt != null)
                {
                    skipped++;
                    continue;
                }

                int? leaveTypeId = null;
                if (codeToLeaveType.TryGetValue(code, out var lt))
                    leaveTypeId = lt.Id;

                db.DailyAttendances.Add(new DailyAttendance
                {
                    EmployeeFinancialNo = finNo,
                    Date = date,
                    Status = status,
                    LeaveTypeId = leaveTypeId
                });

                if (LeaveCodes.Contains(code))
                {
                    var balanceType = CodeToBalanceType.GetValueOrDefault(code);
                    if (balanceType != null && balance != null)
                    {
                        switch (balanceType)
                        {
                            case "Regular": if (balance.RegularLeave >= 1) balance.RegularLeave -= 1; break;
                            case "Casual": if (balance.CasualLeave >= 1) balance.CasualLeave -= 1; break;
                            case "Rest": if (balance.RestAllowance >= 1) balance.RestAllowance -= 1; break;
                        }
                    }

                    db.LeaveTransactions.Add(new LeaveTransaction
                    {
                        EmployeeFinancialNo = finNo,
                        LeaveTypeId = leaveTypeId ?? 0,
                        FromDate = date,
                        ToDate = date,
                        DaysCount = 1,
                        Reason = $"Auto from monthly sheet ({code})",
                        Status = "Approved",
                        CreatedAt = DateTime.Now
                    });
                }

                processed++;
            }
        }

        await db.SaveChangesAsync();
        return $"Processed {processed} records ({skipped} skipped, {records.Count} total) for {year}/{month:D2}";
    }
}
