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
        { "Pending", "قيد الانتظار" },
        { "Checked In", "حاضر بدون انصراف" },
        { "Missing Check Out", "لم يسجل انصراف" },
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
        var employeeNos = await FindEmployeeNumbersAsync(db, new[] { financialNo });

        var records = await db.DailyAttendances
            .AsNoTracking()
            .Include(d => d.LeaveType)
            .Include(d => d.Employee)
            .Where(d => employeeNos.Contains(d.EmployeeFinancialNo) && d.Date == date)
            .ToListAsync();
        await RefreshTransientStatusesAsync(db, records);
        return records;
    }

    public async Task<List<DailyAttendance>> GetEmployeeAttendanceAsync(string financialNo, DateTime fromDate, DateTime toDate)
    {
        using var db = await _factory.CreateDbContextAsync();
        var employeeNos = await FindEmployeeNumbersAsync(db, new[] { financialNo });

        var records = await db.DailyAttendances
            .AsNoTracking()
            .Include(d => d.LeaveType)
            .Include(d => d.Employee)
            .Where(d => employeeNos.Contains(d.EmployeeFinancialNo) && d.Date >= fromDate && d.Date <= toDate)
            .OrderBy(d => d.Date)
            .ToListAsync();
        await RefreshTransientStatusesAsync(db, records);
        return records;
    }

    public async Task<List<DailyAttendance>> GetAllAttendanceAsync(DateTime date)
    {
        using var db = await _factory.CreateDbContextAsync();

        var records = await db.DailyAttendances
            .AsNoTracking()
            .Include(d => d.LeaveType)
            .Include(d => d.Employee)
            .Where(d => d.Date == date)
            .OrderBy(d => d.Employee!.Name)
            .ToListAsync();
        await RefreshTransientStatusesAsync(db, records);
        return records;
    }

    public async Task<List<DailyAttendance>> QueryAttendanceAsync(
        List<string>? employeeNos,
        DateTime fromDate,
        DateTime toDate,
        string? department = null,
        string? level = null,
        string? area = null,
        string? scheduleStart = null,
        string? scheduleEnd = null,
        string? matchMode = "contains")
    {
        using var db = await _factory.CreateDbContextAsync();

        var query = db.DailyAttendances
            .AsNoTracking()
            .Include(d => d.LeaveType)
            .Include(d => d.Employee)
            .Where(d => d.Date >= fromDate && d.Date <= toDate);

        if (employeeNos != null && employeeNos.Count > 0)
        {
            var matchedEmployeeNos = await FindEmployeeNumbersAsync(db, employeeNos, matchMode);
            query = query.Where(d => matchedEmployeeNos.Contains(d.EmployeeFinancialNo));
        }

        if (!string.IsNullOrWhiteSpace(department))
            query = query.Where(d => d.Employee != null && d.Employee.Department == department);

        if (!string.IsNullOrWhiteSpace(level))
            query = query.Where(d => d.Employee != null && d.Employee.Level == level);

        if (!string.IsNullOrWhiteSpace(area))
            query = query.Where(d => d.Employee != null && d.Employee.WorkLocation == area);

        if (!string.IsNullOrWhiteSpace(scheduleStart))
            query = query.Where(d => d.Employee != null && d.Employee.ScheduleStart == scheduleStart);

        if (!string.IsNullOrWhiteSpace(scheduleEnd))
            query = query.Where(d => d.Employee != null && d.Employee.ScheduleEnd == scheduleEnd);

        var records = await query.ToListAsync();
        records = records
            .OrderBy(d => long.TryParse(d.EmployeeFinancialNo, out var fin) ? fin : long.MaxValue)
            .ThenBy(d => d.EmployeeFinancialNo)
            .ThenBy(d => d.Date)
            .ToList();
        await RefreshTransientStatusesAsync(db, records);
        return records;
    }

    public async Task<List<TopManagementDailyRow>> GetTopManagementDailyAsync(DateTime date)
    {
        using var db = await _factory.CreateDbContextAsync();

        var today = date.Date;
        var yesterday = today.AddDays(-1);

        var employees = await db.Employees
            .AsNoTracking()
            .Where(e => e.Level != null && e.Level.Trim() == "اداره عليا")
            .ToListAsync();

        employees = employees
            .OrderBy(e => long.TryParse(e.FinancialNo, out var fin) ? fin : long.MaxValue)
            .ThenBy(e => e.FinancialNo)
            .ToList();

        var financialNumbers = employees.Select(e => e.FinancialNo).ToList();

        var records = await db.DailyAttendances
            .AsNoTracking()
            .Where(d => financialNumbers.Contains(d.EmployeeFinancialNo)
                && (d.Date == today || d.Date == yesterday))
            .ToListAsync();

        var todayByFin = records
            .Where(r => r.Date == today)
            .ToDictionary(r => r.EmployeeFinancialNo, StringComparer.OrdinalIgnoreCase);
        var yesterdayByFin = records
            .Where(r => r.Date == yesterday)
            .ToDictionary(r => r.EmployeeFinancialNo, StringComparer.OrdinalIgnoreCase);

        var rows = new List<TopManagementDailyRow>();
        foreach (var employee in employees)
        {
            yesterdayByFin.TryGetValue(employee.FinancialNo, out var yesterdayRecord);
            todayByFin.TryGetValue(employee.FinancialNo, out var todayRecord);

            var notes = new List<string>();
            if (yesterdayRecord == null || !yesterdayRecord.LastPunch.HasValue)
                notes.Add("لم يسجل انصراف أمس");
            if (todayRecord == null || !todayRecord.FirstPunch.HasValue)
                notes.Add("لم يسجل حضور اليوم");

            rows.Add(new TopManagementDailyRow
            {
                FinancialNo = employee.FinancialNo,
                Name = employee.Name,
                JobTitle = employee.JobTitle,
                Department = employee.Department,
                YesterdayLastPunch = ToEgyptTime(yesterdayRecord?.LastPunch),
                TodayFirstPunch = ToEgyptTime(todayRecord?.FirstPunch),
                MissingYesterdayCheckout = yesterdayRecord == null || !yesterdayRecord.LastPunch.HasValue,
                MissingTodayCheckIn = todayRecord == null || !todayRecord.FirstPunch.HasValue,
                Notes = string.Join(" - ", notes)
            });
        }

        return rows;
    }

    public async Task<AttendanceFilterOptions> GetFilterOptionsAsync()
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

        var schedules = await db.Employees.AsNoTracking()
            .Where(e => e.ScheduleStart != null && e.ScheduleEnd != null)
            .Select(e => new { e.ScheduleStart, e.ScheduleEnd })
            .Distinct()
            .ToListAsync();

        var schedulePairs = schedules
            .Select(s => s.ScheduleStart + " - " + s.ScheduleEnd)
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        return new AttendanceFilterOptions
        {
            Departments = departments,
            Levels = levels,
            Areas = areas,
            Schedules = schedulePairs
        };
    }

    public async Task<List<LeaveBalance>> GetLeaveBalancesAsync(string? financialNo)
    {
        using var db = await _factory.CreateDbContextAsync();

        var query = db.LeaveBalances
            .AsNoTracking()
            .Include(b => b.Employee)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(financialNo))
        {
            var matchedEmployeeNos = await FindEmployeeNumbersAsync(db, new[] { financialNo });
            query = query.Where(b => matchedEmployeeNos.Contains(b.EmployeeFinancialNo));
        }

        return await query
            .OrderBy(b => b.EmployeeFinancialNo)
            .ThenByDescending(b => b.Year)
            .ToListAsync();
    }

    public async Task<string> BulkUpdateStatusAsync(DateTime date, List<BulkStatusUpdate> updates)
    {
        using var db = await _factory.CreateDbContextAsync();
        var updated = 0;

        var financialNumbers = updates
            .Select(update => update.FinancialNo)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var existingRows = await db.DailyAttendances
            .Where(record => record.Date == date && financialNumbers.Contains(record.EmployeeFinancialNo))
            .ToListAsync();
        var existingByEmployee = existingRows
            .ToDictionary(record => record.EmployeeFinancialNo, StringComparer.OrdinalIgnoreCase);

        foreach (var u in updates)
        {
            existingByEmployee.TryGetValue(u.FinancialNo, out var record);

            if (record == null)
            {
                record = new DailyAttendance
                {
                    EmployeeFinancialNo = u.FinancialNo,
                    Date = date,
                    Status = u.Status,
                    LeaveTypeId = u.LeaveTypeId
                };
                db.DailyAttendances.Add(record);
                existingByEmployee[u.FinancialNo] = record;
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

    public async Task<AttendanceRecalculationResult> RecalculateStatusesAsync(
        DateTime fromDate,
        DateTime toDate,
        List<string>? employeeTerms,
        string? department,
        string? level,
        string? area,
        string? scheduleStart,
        string? scheduleEnd,
        string? matchMode = "contains")
    {
        fromDate = fromDate.Date;
        toDate = toDate.Date;
        if (toDate < fromDate)
            throw new ArgumentException("End date must be on or after start date.");
        if ((toDate - fromDate).TotalDays > 366)
            throw new ArgumentException("Attendance can be recalculated for a maximum of 367 days at a time.");

        using var db = await _factory.CreateDbContextAsync();
        var calendarSettings = await db.AttendanceDaySettings.AsNoTracking().ToListAsync();
        var scheduleRules = await db.ScheduleRules.AsNoTracking().Where(rule => rule.IsActive).ToListAsync();
        var query = db.DailyAttendances
            .Include(record => record.Employee)
            .Where(record => record.Date >= fromDate && record.Date <= toDate);

        if (employeeTerms is { Count: > 0 })
        {
            var matched = await FindEmployeeNumbersAsync(db, employeeTerms, matchMode);
            query = query.Where(record => matched.Contains(record.EmployeeFinancialNo));
        }
        if (!string.IsNullOrWhiteSpace(department))
            query = query.Where(record => record.Employee != null && record.Employee.Department == department);
        if (!string.IsNullOrWhiteSpace(level))
            query = query.Where(record => record.Employee != null && record.Employee.Level == level);
        if (!string.IsNullOrWhiteSpace(area))
            query = query.Where(record => record.Employee != null && record.Employee.WorkLocation == area);
        if (!string.IsNullOrWhiteSpace(scheduleStart))
            query = query.Where(record => record.Employee != null && record.Employee.ScheduleStart == scheduleStart);
        if (!string.IsNullOrWhiteSpace(scheduleEnd))
            query = query.Where(record => record.Employee != null && record.Employee.ScheduleEnd == scheduleEnd);

        var records = await query.ToListAsync();
        var updated = 0;
        var unchanged = 0;
        var skipped = 0;
        var now = DateTime.Now;

        foreach (var record in records)
        {
            if (record.LeaveTypeId.HasValue || !AttendanceStatusRules.IsAutomaticStatus(record.Status))
            {
                skipped++;
                continue;
            }

            var currentSchedule = record.Employee == null
                ? null
                : AttendanceStatusRules.ResolveSchedule(record.Employee, scheduleRules);
            var scheduledStartValue = currentSchedule?.StartTime ?? record.ScheduledStart;
            var scheduledEndValue = currentSchedule?.EndTime ?? record.ScheduledEnd;
            var status = AttendanceStatusRules.Calculate(
                record.FirstPunch,
                record.LastPunch,
                scheduledStartValue,
                scheduledEndValue,
                record.Date,
                calendarSettings,
                now);

            if (string.Equals(record.Status, status, StringComparison.Ordinal)
                && string.Equals(record.ScheduledStart, scheduledStartValue, StringComparison.Ordinal)
                && string.Equals(record.ScheduledEnd, scheduledEndValue, StringComparison.Ordinal))
            {
                unchanged++;
                continue;
            }

            record.Status = status;
            record.ScheduledStart = scheduledStartValue;
            record.ScheduledEnd = scheduledEndValue;
            updated++;
        }

        if (updated > 0)
            await db.SaveChangesAsync();

        return new AttendanceRecalculationResult(records.Count, updated, unchanged, skipped);
    }

    public static string GetStatusArabic(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return "-";
        return StatusAr.TryGetValue(status, out var ar) ? ar : status;
    }

    private static async Task RefreshTransientStatusesAsync(
        AppDbContext db,
        IEnumerable<DailyAttendance> records)
    {
        var calendarSettings = await db.AttendanceDaySettings
            .AsNoTracking()
            .ToListAsync();
        var now = DateTime.Now;

        foreach (var record in records)
            AttendanceStatusRules.RefreshTransientStatus(record, calendarSettings, now);
    }

    private static DateTime? ToEgyptTime(DateTime? utc)
    {
        if (!utc.HasValue) return null;
        return new DateTime(utc.Value.Ticks + TimeSpan.TicksPerHour * 2, DateTimeKind.Unspecified);
    }

    private static async Task<List<string>> FindEmployeeNumbersAsync(
        AppDbContext db,
        IEnumerable<string> searchTerms,
        string? matchMode = "contains")
    {
        var terms = searchTerms
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (terms.Count == 0)
            return new List<string>();

        var isExact = string.Equals(matchMode, "exact", StringComparison.OrdinalIgnoreCase);
        var isOneOf = string.Equals(matchMode, "oneof", StringComparison.OrdinalIgnoreCase);

        var employees = await db.Employees
            .AsNoTracking()
            .Select(e => new { e.FinancialNo, e.Name })
            .ToListAsync();

        return employees
            .Where(e => terms.Any(term =>
            {
                if (isOneOf)
                    return e.FinancialNo.Equals(term, StringComparison.OrdinalIgnoreCase);

                if (isExact)
                    return e.FinancialNo.Equals(term, StringComparison.OrdinalIgnoreCase)
                        || (!string.IsNullOrWhiteSpace(e.Name) && e.Name.Equals(term, StringComparison.OrdinalIgnoreCase));

                return e.FinancialNo.Equals(term, StringComparison.OrdinalIgnoreCase)
                    || e.FinancialNo.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || (!string.IsNullOrWhiteSpace(e.Name) && e.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
            }))
            .Select(e => e.FinancialNo)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

public class BulkStatusUpdate
{
    public string FinancialNo { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? LeaveTypeId { get; set; }
}

public class AttendanceFilterOptions
{
    public List<string> Departments { get; set; } = new();
    public List<string> Levels { get; set; } = new();
    public List<string> Areas { get; set; } = new();
    public List<string> Schedules { get; set; } = new();
}

public class TopManagementDailyRow
{
    public string FinancialNo { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public string? Department { get; set; }
    public DateTime? YesterdayLastPunch { get; set; }
    public DateTime? TodayFirstPunch { get; set; }
    public bool MissingYesterdayCheckout { get; set; }
    public bool MissingTodayCheckIn { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public sealed record AttendanceRecalculationResult(
    int ProcessedCount,
    int UpdatedCount,
    int UnchangedCount,
    int SkippedCount);
