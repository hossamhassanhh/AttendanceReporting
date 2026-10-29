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
            query = AttendanceStatusRules.IsStandardJobLevelsGroup(level)
                ? query.Where(d => d.Employee != null && d.Employee.Level != null && AttendanceStatusRules.StandardJobLevels.Contains(d.Employee.Level))
                : query.Where(d => d.Employee != null && d.Employee.Level == level);

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

    public async Task<Dictionary<int, LateAllowanceSnapshot>> BuildMonthlyLateAllowanceAsync(
        IReadOnlyCollection<DailyAttendance> queriedRecords)
    {
        var snapshots = new Dictionary<int, LateAllowanceSnapshot>();
        if (queriedRecords.Count == 0)
            return snapshots;

        var keys = queriedRecords
            .Select(r => (FinancialNo: r.EmployeeFinancialNo, r.Date.Year, r.Date.Month))
            .Distinct()
            .ToList();
        var employeeNos = keys
            .Select(k => k.FinancialNo)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var rangeStart = keys.Min(k => new DateTime(k.Year, k.Month, 1));
        var rangeEnd = keys.Max(k => new DateTime(k.Year, k.Month, 1).AddMonths(1).AddDays(-1));

        // NOTE: snapshots mirror the status walk exactly (same quota consumption
        // in date order), so the displayed remaining quotas always reconcile
        // with the report Status and the X/X1 codes.
        List<DailyAttendance> monthRecords;
        List<AttendanceDaySetting> calendarSettings;
        MonthlyStatusContext statusContext;
        using (var db = await _factory.CreateDbContextAsync())
        {
            monthRecords = await db.DailyAttendances
                .AsNoTracking()
                .Where(d => d.Date >= rangeStart
                    && d.Date <= rangeEnd
                    && employeeNos.Contains(d.EmployeeFinancialNo))
                .ToListAsync();

            calendarSettings = await db.AttendanceDaySettings
                .AsNoTracking()
                .Where(s => s.Date >= rangeStart && s.Date <= rangeEnd)
                .ToListAsync();

            statusContext = await LoadMonthlyStatusContextAsync(db, employeeNos, rangeStart, rangeEnd);
        }

        var outcomes = AttendanceStatusRules.CalculateMonthlyStatuses(
            monthRecords,
            statusContext.LeaveFlags,
            calendarSettings,
            statusContext.LeaveTypeCodes);

        foreach (var monthGroup in monthRecords.GroupBy(r => new
                 {
                     r.EmployeeFinancialNo,
                     r.Date.Year,
                     r.Date.Month
                 }))
        {
            var key = (monthGroup.Key.EmployeeFinancialNo, monthGroup.Key.Year, monthGroup.Key.Month);
            if (!keys.Contains(key))
                continue;

            var used = 0;
            var graceUsed = 0;
            foreach (var record in monthGroup.OrderBy(r => r.Date).ThenBy(r => r.Id))
            {
                var dailyHours = 0;
                if (outcomes.TryGetValue(record.Id, out var outcome))
                {
                    graceUsed += outcome.GraceConsumedMinutes;
                    used += outcome.LateHoursConsumed;
                    if (outcome.DailyLateMinutes > 0)
                        dailyHours = (outcome.DailyLateMinutes + 59) / 60;
                }

                snapshots[record.Id] = new LateAllowanceSnapshot(
                    dailyHours,
                    used,
                    Math.Max(0, AttendanceStatusRules.LatePermissionMonthlyHours - used),
                    graceUsed,
                    Math.Max(0, AttendanceStatusRules.GraceLateAllowanceMinutes - graceUsed));
            }
        }

        return snapshots;
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

    public async Task<List<TopManagementOvertimeRow>> GetTopManagementOvertimeAsync(DateTime fromDate, DateTime toDate)
    {
        using var db = await _factory.CreateDbContextAsync();

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
                && d.Date >= fromDate.Date && d.Date <= toDate.Date)
            .ToListAsync();

        var overtimeThreshold = TimeSpan.FromHours(15.5); // 15:30
        var rows = new List<TopManagementOvertimeRow>();

        foreach (var employee in employees)
        {
            var totalMinutes = 0;
            var overtimeDays = 0;

            foreach (var record in records.Where(r => r.EmployeeFinancialNo == employee.FinancialNo))
            {
                if (!record.LastPunch.HasValue)
                    continue;

                var lastPunchEgypt = ToEgyptTime(record.LastPunch.Value);
                if (!lastPunchEgypt.HasValue)
                    continue;

                var punchTime = lastPunchEgypt.Value.TimeOfDay;
                if (punchTime > overtimeThreshold)
                {
                    totalMinutes += (int)(punchTime - overtimeThreshold).TotalMinutes;
                    overtimeDays++;
                }
            }

            rows.Add(new TopManagementOvertimeRow
            {
                FinancialNo = employee.FinancialNo,
                Name = employee.Name,
                JobTitle = employee.JobTitle,
                Department = employee.Department,
                OvertimeDays = overtimeDays,
                OvertimeMinutes = totalMinutes
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
            query = AttendanceStatusRules.IsStandardJobLevelsGroup(level)
                ? query.Where(record => record.Employee != null && record.Employee.Level != null && AttendanceStatusRules.StandardJobLevels.Contains(record.Employee.Level))
                : query.Where(record => record.Employee != null && record.Employee.Level == level);
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

        var permissionIdSet = await db.LeaveTypes
            .AsNoTracking()
            .Where(t => t.Code == "P" || t.Code == "PL" || t.Code == "PE")
            .Select(t => t.Id)
            .ToHashSetAsync();

        var processed = new List<(DailyAttendance Record, string Status, string? ScheduledStart, string? ScheduledEnd)>();
        foreach (var record in records)
        {
            if (!AttendanceStatusRules.ShouldRecalculate(record, permissionIdSet))
            {
                skipped++;
                continue;
            }

            var currentSchedule = record.Employee == null
                ? null
                : AttendanceStatusRules.ResolveSchedule(record.Employee, scheduleRules);
            var scheduledStartValue = currentSchedule?.StartTime ?? record.ScheduledStart;
            var scheduledEndValue = currentSchedule?.EndTime ?? record.ScheduledEnd;

            processed.Add((record, record.Status, record.ScheduledStart, record.ScheduledEnd));
            record.ScheduledStart = scheduledStartValue;
            record.ScheduledEnd = scheduledEndValue;
        }

        Dictionary<int, MonthlyStatusOutcome> outcomes = new();
        if (processed.Count > 0)
        {
            // Evaluate whole months in date order so the 30-minute grace and the
            // 2-hour late-permission quotas are consumed chronologically.
            var employeeNos = processed
                .Select(p => p.Record.EmployeeFinancialNo)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var rangeStart = processed.Min(p => new DateTime(p.Record.Date.Year, p.Record.Date.Month, 1));
            var rangeEnd = processed.Max(p => new DateTime(p.Record.Date.Year, p.Record.Date.Month, 1).AddMonths(1).AddDays(-1));

            var monthRecords = await db.DailyAttendances
                .Where(record => record.Date >= rangeStart
                    && record.Date <= rangeEnd
                    && employeeNos.Contains(record.EmployeeFinancialNo))
                .ToListAsync();
            var statusContext = await LoadMonthlyStatusContextAsync(db, employeeNos, rangeStart, rangeEnd);

            outcomes = AttendanceStatusRules.CalculateMonthlyStatuses(
                monthRecords,
                statusContext.LeaveFlags,
                calendarSettings,
                statusContext.LeaveTypeCodes,
                now);
        }

        foreach (var (record, previousStatus, previousStart, previousEnd) in processed)
        {
            if (!outcomes.TryGetValue(record.Id, out var outcome))
            {
                outcome = new MonthlyStatusOutcome(
                    AttendanceStatusRules.Calculate(
                        record.FirstPunch,
                        record.LastPunch,
                        record.ScheduledStart,
                        record.ScheduledEnd,
                        record.Date,
                        calendarSettings,
                        now),
                    0,
                    0,
                    0);
            }

            if (string.Equals(outcome.Status, previousStatus, StringComparison.Ordinal)
                && string.Equals(record.ScheduledStart, previousStart, StringComparison.Ordinal)
                && string.Equals(record.ScheduledEnd, previousEnd, StringComparison.Ordinal))
            {
                unchanged++;
                continue;
            }

            record.Status = outcome.Status;
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
        var recordList = records as IReadOnlyList<DailyAttendance> ?? records.ToList();
        if (recordList.Count == 0)
            return;

        var rangeStart = recordList.Min(r => new DateTime(r.Date.Year, r.Date.Month, 1));
        var rangeEnd = recordList.Max(r => new DateTime(r.Date.Year, r.Date.Month, 1).AddMonths(1).AddDays(-1));
        var employeeNos = recordList
            .Select(r => r.EmployeeFinancialNo)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var calendarSettings = await db.AttendanceDaySettings
            .AsNoTracking()
            .Where(s => s.Date >= rangeStart && s.Date <= rangeEnd)
            .ToListAsync();
        var statusContext = await LoadMonthlyStatusContextAsync(db, employeeNos, rangeStart, rangeEnd);

        // Walk whole months in date order so the grace / late-permission quotas
        // are consumed chronologically, exactly like the stored recalculation.
        var monthRecords = await db.DailyAttendances
            .AsNoTracking()
            .Where(d => d.Date >= rangeStart
                && d.Date <= rangeEnd
                && employeeNos.Contains(d.EmployeeFinancialNo))
            .ToListAsync();

        var outcomes = AttendanceStatusRules.CalculateMonthlyStatuses(
            monthRecords,
            statusContext.LeaveFlags,
            calendarSettings,
            statusContext.LeaveTypeCodes,
            DateTime.Now);

        foreach (var record in recordList)
        {
            if (outcomes.TryGetValue(record.Id, out var outcome))
                record.Status = outcome.Status;
        }
    }

    /// <summary>
    /// Loads the approved leave/permission flags per date and the leave type
    /// codes needed by the month-ordered status walk.
    /// </summary>
    public static async Task<MonthlyStatusContext> LoadMonthlyStatusContextAsync(
        AppDbContext db,
        IReadOnlyCollection<string> employeeNos,
        DateTime rangeStart,
        DateTime rangeEnd)
    {
        var leaveTypeCodes = await db.LeaveTypes
            .AsNoTracking()
            .ToDictionaryAsync(t => t.Id, t => t.Code);

        var transactions = await db.LeaveTransactions
            .AsNoTracking()
            .Include(t => t.LeaveType)
            .Where(t => t.Status == "Approved"
                && t.FromDate <= rangeEnd
                && t.ToDate >= rangeStart
                && employeeNos.Contains(t.EmployeeFinancialNo))
            .ToListAsync();

        var leaveFlags = new Dictionary<(string EmployeeFinancialNo, DateTime Date), DayLeaveFlags>();
        foreach (var transaction in transactions)
        {
            var latePermission = string.Equals(transaction.LeaveType?.Code, "PL", StringComparison.OrdinalIgnoreCase);
            var from = transaction.FromDate.Date < rangeStart ? rangeStart : transaction.FromDate.Date;
            var to = transaction.ToDate.Date > rangeEnd ? rangeEnd : transaction.ToDate.Date;
            for (var day = from; day <= to; day = day.AddDays(1))
            {
                var key = (transaction.EmployeeFinancialNo, day);
                leaveFlags.TryGetValue(key, out var existing);
                leaveFlags[key] = new DayLeaveFlags(true, existing.LatePermission || latePermission);
            }
        }

        return new MonthlyStatusContext(leaveFlags, leaveTypeCodes);
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

public class TopManagementOvertimeRow
{
    public string FinancialNo { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public string? Department { get; set; }
    public int OvertimeDays { get; set; }
    public int OvertimeMinutes { get; set; }
    public string OvertimeFormatted => OvertimeMinutes > 0 ? $"{OvertimeMinutes / 60}:{OvertimeMinutes % 60:D2}" : "-";
}

public sealed record LateAllowanceSnapshot(
    int DailyHours,
    int UsedHours,
    int RemainingHours,
    int GraceUsedMinutes,
    int GraceRemainingMinutes);

public sealed record MonthlyStatusContext(
    Dictionary<(string EmployeeFinancialNo, DateTime Date), DayLeaveFlags> LeaveFlags,
    Dictionary<int, string> LeaveTypeCodes);

public sealed record AttendanceRecalculationResult(
    int ProcessedCount,
    int UpdatedCount,
    int UnchangedCount,
    int SkippedCount);
