using AttendanceApp.Models;

namespace AttendanceApp.Services;

public static class AttendanceStatusRules
{
    public const int CheckOutGraceMinutes = 30;
    public const int GraceLateAllowanceMinutes = 30;
    public const int LatePermissionMonthlyHours = 2;
    public const int EarlyLeavePermissionMonthlyCount = 2;
    public static readonly TimeSpan EarlyLeavePermissionStart = new(10, 0, 0);

    private static readonly HashSet<string> AutomaticStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Unknown",
        "Pending",
        "Present",
        "Checked In",
        "Missing Check Out",
        "Late",
        "Early Leave",
        "Absent",
        "Work From Home"
    };

    private static readonly HashSet<string> PermissionLeaveCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "P",
        "PL",
        "PE"
    };

    public static string Calculate(
        DateTime? firstPunch,
        DateTime? lastPunch,
        string? scheduledStart,
        string? scheduledEnd,
        DateTime date,
        IReadOnlyCollection<AttendanceDaySetting>? calendarSettings,
        DateTime? now = null,
        MonthlyStatusQuota? quota = null,
        bool enteredLeave = false,
        bool latePermissionLeave = false,
        bool earlyLeavePermissionLeave = false)
    {
        var configuredDayStatus = AttendanceCalendarRules.GetDayStatus(date, calendarSettings);
        if (configuredDayStatus != null)
            return configuredDayStatus;

        var baseStatus = CalculateBase(firstPunch, lastPunch, scheduledStart, scheduledEnd, date, now);
        var (status, _) = ApplyStatusRules(baseStatus, firstPunch, lastPunch, scheduledStart, date, quota, enteredLeave, latePermissionLeave, earlyLeavePermissionLeave);
        return status;
    }

    /// <summary>
    /// Punch-only evaluation: calendar overrides, pending/absent handling and the
    /// late / early-leave classification, without the monthly quota rules.
    /// </summary>
    public static string CalculateBase(
        DateTime? firstPunch,
        DateTime? lastPunch,
        string? scheduledStart,
        string? scheduledEnd,
        DateTime date,
        DateTime? now = null)
    {
        var currentTime = now ?? DateTime.Now;
        var startTime = TimeSpan.Zero;
        var endTime = TimeSpan.Zero;
        var hasSchedule = TimeSpan.TryParse(scheduledStart, out startTime)
            && TimeSpan.TryParse(scheduledEnd, out endTime);
        var scheduledEndTime = hasSchedule ? date.Date.Add(endTime) : date.Date.AddDays(1);
        var finalizationTime = scheduledEndTime.AddMinutes(CheckOutGraceMinutes);

        if (!firstPunch.HasValue)
            return date.Date == currentTime.Date && currentTime < finalizationTime
                ? "Pending"
                : "Absent";

        lastPunch ??= firstPunch;

        // A single valid punch proves presence, but cannot prove a completed shift.
        if (firstPunch.Value == lastPunch.Value)
            return date.Date == currentTime.Date && currentTime < finalizationTime
                ? "Checked In"
                : "Missing Check Out";

        if (!hasSchedule)
            return "Present";

        var firstPunchLocal = AsEgyptTime(firstPunch.Value);
        var lastPunchLocal = AsEgyptTime(lastPunch.Value);
        var scheduledStartTime = date.Date.Add(startTime);
        var workedMinutes = (lastPunchLocal - firstPunchLocal).TotalMinutes;
        var requiredMinutes = (scheduledEndTime - scheduledStartTime).TotalMinutes;

        if (workedMinutes < requiredMinutes * 0.5)
            return "Absent";

        // Early leave must not be hidden by the late-allowance credit, so evaluate
        // it before late arrival: a day with an early check-out is "Early Leave".
        if (lastPunchLocal < scheduledEndTime)
            return "Early Leave";

        if (firstPunchLocal > scheduledStartTime)
            return "Late";

        return "Present";
    }

    /// <summary>
    /// Applies the monthly grace / late-permission / early-leave rules to a
    /// punch-derived status. Returns the effective status plus whether an
    /// automatic early check-out was covered by the early-leave permission.
    /// </summary>
    private static (string Status, bool EarlyLeaveConsumed) ApplyStatusRules(
        string baseStatus,
        DateTime? firstPunch,
        DateTime? lastPunch,
        string? scheduledStart,
        DateTime date,
        MonthlyStatusQuota? quota,
        bool enteredLeave,
        bool latePermissionLeave,
        bool earlyLeavePermissionLeave)
    {
        if (string.Equals(baseStatus, "Late", StringComparison.Ordinal))
        {
            // An entered late permission (PL) excuses the arrival and spends the
            // monthly 2-hour late-permission quota (rounded up to whole hours);
            // the 30-minute grace is never consumed on a PL day.
            if (latePermissionLeave)
            {
                if (quota != null)
                    quota.ConsumeLateHoursUpTo((GetLateMinutes(firstPunch, scheduledStart, date) + 59) / 60);
                return ("Present", false);
            }

            var lateMinutes = GetLateMinutes(firstPunch, scheduledStart, date);

            // Days covered by an entered leave consume nothing: the monthly codes
            // treat those days as leave, so the quotas must not be spent on them.
            var consume = !enteredLeave;
            if (quota != null && quota.TryConsumeGrace(lateMinutes, consume))
                return ("Present", false);
            if (quota != null && quota.TryConsumeLateHours((lateMinutes + 59) / 60, consume))
                return ("Present", false);
            return ("Late", false);
        }

        if (string.Equals(baseStatus, "Early Leave", StringComparison.Ordinal))
        {
            if (earlyLeavePermissionLeave)
            {
                // An entered early-leave permission (PE) excuses the early
                // check-out and consumes one of the monthly early-leave
                // permissions when quota allows; an entered permission always
                // excuses the day.
                var consumed = lastPunch.HasValue && quota != null
                    && quota.TryConsumeEarlyLeave(AttendanceStatusRules.ToEgyptTime(lastPunch.Value).TimeOfDay);
                return (enteredLeave || consumed ? "Present" : "Early Leave", consumed);
            }

            // An entered leave/permission excuses an early check-out.
            if (enteredLeave)
                return ("Present", false);

            // Automatic early check-out: the first two per month with a check-out
            // at or after 10:00 become present and consume the early-leave
            // permission; later ones stay early leaves.
            var automaticConsumed = lastPunch.HasValue && quota != null
                && quota.TryConsumeEarlyLeave(AttendanceStatusRules.ToEgyptTime(lastPunch.Value).TimeOfDay);
            return (automaticConsumed ? "Present" : "Early Leave", automaticConsumed);
        }

        return (baseStatus, false);
    }

    /// <summary>
    /// Walks one set of records (an employee-month) in date order, applying the
    /// monthly 30-minute grace quota and the automatic 2-hour late-permission
    /// quota, and returns the resulting status plus quota consumption per record.
    /// </summary>
    public static Dictionary<int, MonthlyStatusOutcome> CalculateMonthlyStatuses(
        IEnumerable<DailyAttendance> records,
        IReadOnlyDictionary<(string EmployeeFinancialNo, DateTime Date), DayLeaveFlags>? leaveFlags,
        IReadOnlyCollection<AttendanceDaySetting>? calendarSettings,
        IReadOnlyDictionary<int, string>? leaveTypeCodes,
        DateTime? now = null)
    {
        var outcomes = new Dictionary<int, MonthlyStatusOutcome>();
        var monthGroups = records.GroupBy(r => new { r.EmployeeFinancialNo, r.Date.Year, r.Date.Month });

        foreach (var monthGroup in monthGroups)
        {
            var quota = new MonthlyStatusQuota();

            foreach (var record in monthGroup.OrderBy(r => r.Date).ThenBy(r => r.Id))
            {
                DayLeaveFlags transactionFlags = default;
                leaveFlags?.TryGetValue((record.EmployeeFinancialNo, record.Date.Date), out transactionFlags);
                var enteredLeave = transactionFlags.EnteredLeave;
                var latePermissionLeave = transactionFlags.LatePermission;
                var earlyLeavePermissionLeave = transactionFlags.EarlyLeavePermission;

                if (record.LeaveTypeId.HasValue)
                {
                    var code = leaveTypeCodes != null
                        && leaveTypeCodes.TryGetValue(record.LeaveTypeId.Value, out var leaveTypeCode)
                            ? leaveTypeCode
                            : null;

                    // Regular leave days keep their stored status and consume nothing.
                    if (!IsPermissionCode(code))
                        continue;

                    enteredLeave = true;
                    latePermissionLeave = latePermissionLeave || IsLatePermissionCode(code);
                    earlyLeavePermissionLeave = earlyLeavePermissionLeave || IsEarlyLeavePermissionCode(code);

                    var permissionDayStatus = AttendanceCalendarRules.GetDayStatus(record.Date, calendarSettings);
                    if (permissionDayStatus != null)
                    {
                        outcomes[record.Id] = new MonthlyStatusOutcome(record.Status, 0, 0, 0);
                        continue;
                    }

                    var permissionBase = CalculateBase(
                        record.FirstPunch,
                        record.LastPunch,
                        record.ScheduledStart,
                        record.ScheduledEnd,
                        record.Date,
                        now);

                    var graceBefore = quota.GraceMinutesRemaining;
                    var hoursBefore = quota.LateHoursRemaining;
                    var (permissionStatus, permissionEarlyLeaveConsumed) = ApplyStatusRules(
                        permissionBase,
                        record.FirstPunch,
                        record.LastPunch,
                        record.ScheduledStart,
                        record.Date,
                        quota,
                        enteredLeave,
                        latePermissionLeave,
                        earlyLeavePermissionLeave);

                    if (string.Equals(permissionBase, "Late", StringComparison.Ordinal) && latePermissionLeave)
                    {
                        // Entered late permission (PL): the arrival is excused and the
                        // monthly 2-hour late-permission quota is spent on it.
                        outcomes[record.Id] = new MonthlyStatusOutcome(
                            permissionStatus,
                            0,
                            graceBefore - quota.GraceMinutesRemaining,
                            hoursBefore - quota.LateHoursRemaining,
                            permissionEarlyLeaveConsumed);
                        continue;
                    }

                    if (string.Equals(permissionBase, "Late", StringComparison.Ordinal))
                    {
                        // Entered general permission (P): the day is excused and
                        // consumes no late quota.
                        outcomes[record.Id] = new MonthlyStatusOutcome("Present", 0, 0, 0, false);
                        continue;
                    }

                    if (string.Equals(permissionBase, "Early Leave", StringComparison.Ordinal)
                        || string.Equals(permissionBase, "Present", StringComparison.Ordinal))
                    {
                        // Attended with an entered permission: considered present. An
                        // entered early-leave permission (PE) consumes one of the
                        // monthly early-leave permissions.
                        outcomes[record.Id] = new MonthlyStatusOutcome("Present", 0, 0, 0, permissionEarlyLeaveConsumed);
                        continue;
                    }

                    // No proof of a full day: the entered permission stays "Leave".
                    outcomes[record.Id] = new MonthlyStatusOutcome(record.Status, 0, 0, 0, false);
                    continue;
                }

                if (!IsAutomaticStatus(record.Status))
                    continue;

                var configuredDayStatus = AttendanceCalendarRules.GetDayStatus(record.Date, calendarSettings);
                if (configuredDayStatus != null)
                {
                    outcomes[record.Id] = new MonthlyStatusOutcome(configuredDayStatus, 0, 0, 0);
                    continue;
                }

                var baseStatus = CalculateBase(
                    record.FirstPunch,
                    record.LastPunch,
                    record.ScheduledStart,
                    record.ScheduledEnd,
                    record.Date,
                    now);

                var graceBeforeDay = quota.GraceMinutesRemaining;
                var hoursBeforeDay = quota.LateHoursRemaining;
                var (newStatus, earlyLeaveConsumed) = ApplyStatusRules(
                    baseStatus,
                    record.FirstPunch,
                    record.LastPunch,
                    record.ScheduledStart,
                    record.Date,
                    quota,
                    enteredLeave,
                    latePermissionLeave,
                    earlyLeavePermissionLeave);

                var dailyLateMinutes = string.Equals(baseStatus, "Late", StringComparison.Ordinal)
                    ? GetLateMinutes(record)
                    : 0;

                outcomes[record.Id] = new MonthlyStatusOutcome(
                    newStatus,
                    dailyLateMinutes,
                    graceBeforeDay - quota.GraceMinutesRemaining,
                    hoursBeforeDay - quota.LateHoursRemaining,
                    earlyLeaveConsumed);
            }
        }

        return outcomes;
    }

    /// <summary>
    /// Whether a stored record may be (re)calculated: automatic punch-derived
    /// statuses plus the entered permission leave types (P / PL / PE).
    /// </summary>
    public static bool ShouldRecalculate(DailyAttendance record, IReadOnlySet<int> permissionTypeIds) =>
        record.LeaveTypeId.HasValue
            ? permissionTypeIds.Contains(record.LeaveTypeId.Value)
            : IsAutomaticStatus(record.Status);

    public static void RefreshTransientStatus(
        DailyAttendance attendance,
        IReadOnlyCollection<AttendanceDaySetting>? calendarSettings,
        DateTime? now = null)
    {
        if (attendance.LeaveTypeId.HasValue || !AutomaticStatuses.Contains(attendance.Status))
            return;

        attendance.Status = Calculate(
            attendance.FirstPunch,
            attendance.LastPunch,
            attendance.ScheduledStart,
            attendance.ScheduledEnd,
            attendance.Date,
            calendarSettings,
            now);
    }

    public static int GetLateMinutes(DailyAttendance attendance) =>
        GetLateMinutes(attendance.FirstPunch, attendance.ScheduledStart, attendance.Date);

    private static int GetLateMinutes(DateTime? firstPunch, string? scheduledStart, DateTime date)
    {
        if (!firstPunch.HasValue
            || !TimeSpan.TryParse(scheduledStart, out var scheduledStartValue))
            return 0;

        var checkIn = AsEgyptTime(firstPunch.Value);
        var scheduled = date.Date.Add(scheduledStartValue);
        var lateMinutes = (checkIn - scheduled).TotalMinutes;
        return lateMinutes > 0 ? (int)Math.Ceiling(lateMinutes) : 0;
    }

    private static bool IsPermissionCode(string? code) =>
        code != null && PermissionLeaveCodes.Contains(code);

    /// <summary>True for the entered permission leave types P / PL / PE.</summary>
    public static bool IsPermissionLeaveCode(string? code) => IsPermissionCode(code);

    /// <summary>True for the entered late permission leave type (PL).</summary>
    public static bool IsLatePermissionCode(string? code) =>
        string.Equals(code, "PL", StringComparison.OrdinalIgnoreCase);

    /// <summary>True for the entered early-leave permission type (PE).</summary>
    public static bool IsEarlyLeavePermissionCode(string? code) =>
        string.Equals(code, "PE", StringComparison.OrdinalIgnoreCase);

    public static bool IsAutomaticStatus(string? status) =>
        !string.IsNullOrWhiteSpace(status) && AutomaticStatuses.Contains(status);

    public const string StandardJobLevelsGroupName = "وظائف نمطية";

    public static readonly string[] StandardJobLevels =
    {
        "المستوى الاول",
        "المستوى الثانى",
        "المستوى الثالث"
    };

    public static bool IsStandardJobLevelsGroup(string? level) =>
        string.Equals(level?.Trim(), StandardJobLevelsGroupName, StringComparison.Ordinal);

    public static ScheduleRule? ResolveSchedule(
        Employee employee,
        IReadOnlyCollection<ScheduleRule> rules)
    {
        var level = employee.Level?.Trim().ToLowerInvariant() ?? string.Empty;
        ScheduleRule? schedule = null;

        if (level.Contains("ladies") || level.Contains("سيدات"))
            schedule = rules.FirstOrDefault(rule => rule.LevelName == "Ladies");
        if (level.Contains("top") || level.Contains("عليا") || level.Contains("اداره عليا"))
            schedule ??= rules.FirstOrDefault(rule => rule.LevelName == "Top Management");
        if (level.Contains("level 1") || level.Contains("اول") || level.Contains("الأول"))
            schedule ??= rules.FirstOrDefault(rule => rule.LevelName == "Level 1");
        if (level.Contains("level 2") || level.Contains("ثان") || level.Contains("الثانى"))
            schedule ??= rules.FirstOrDefault(rule => rule.LevelName == "Level 2");
        if (level.Contains("level 3") || level.Contains("ثال") || level.Contains("الثالث"))
            schedule ??= rules.FirstOrDefault(rule => rule.LevelName == "Level 3");

        if (string.IsNullOrWhiteSpace(employee.ScheduleStart)
            && string.IsNullOrWhiteSpace(employee.ScheduleEnd))
            return schedule;

        return new ScheduleRule
        {
            LevelName = schedule?.LevelName ?? "Employee Override",
            StartTime = employee.ScheduleStart ?? schedule?.StartTime ?? "08:31",
            EndTime = employee.ScheduleEnd ?? schedule?.EndTime ?? "15:25",
            IsActive = true
        };
    }

    private static DateTime AsEgyptTime(DateTime value)
    {
        var utc = DateTime.SpecifyKind(value, DateTimeKind.Utc);
        return utc.AddHours(2);
    }

    public static DateTime ToEgyptTime(DateTime value) => AsEgyptTime(value);
}

/// <summary>
/// Month-scoped quota state consumed in date order: the free 30-minute grace
/// quota first, then the automatic 2-hour late-permission quota, then the
/// monthly early-leave permissions.
/// </summary>
public sealed class MonthlyStatusQuota
{
    public int GraceMinutesRemaining { get; private set; } = AttendanceStatusRules.GraceLateAllowanceMinutes;

    public int LateHoursRemaining { get; private set; } = AttendanceStatusRules.LatePermissionMonthlyHours;

    public int EarlyLeavesRemaining { get; private set; } = AttendanceStatusRules.EarlyLeavePermissionMonthlyCount;

    public int GraceMinutesUsed => AttendanceStatusRules.GraceLateAllowanceMinutes - GraceMinutesRemaining;

    public int LateHoursUsed => AttendanceStatusRules.LatePermissionMonthlyHours - LateHoursRemaining;

    public int EarlyLeavesUsed => AttendanceStatusRules.EarlyLeavePermissionMonthlyCount - EarlyLeavesRemaining;

    public bool TryConsumeGrace(int minutes, bool consume = true)
    {
        if (minutes <= 0)
            return true;
        if (minutes > GraceMinutesRemaining)
            return false;
        if (consume)
            GraceMinutesRemaining -= minutes;
        return true;
    }

    public bool TryConsumeLateHours(int hours, bool consume = true)
    {
        if (hours <= 0)
            return true;
        if (hours > LateHoursRemaining)
            return false;
        if (consume)
            LateHoursRemaining -= hours;
        return true;
    }

    /// <summary>Spends late hours from the monthly 2-hour quota, clamped to what is left.</summary>
    public int ConsumeLateHoursUpTo(int hours)
    {
        var consumed = Math.Min(Math.Max(0, hours), LateHoursRemaining);
        LateHoursRemaining -= consumed;
        return consumed;
    }

    public bool TryConsumeEarlyLeave(TimeSpan checkoutTime)
    {
        if (checkoutTime < AttendanceStatusRules.EarlyLeavePermissionStart)
            return false;
        if (EarlyLeavesRemaining <= 0)
            return false;
        EarlyLeavesRemaining--;
        return true;
    }
}

/// <summary>Approved leave/permission covering a specific date.</summary>
public readonly record struct DayLeaveFlags(bool EnteredLeave, bool LatePermission, bool EarlyLeavePermission = false)
{
    public bool HasEarlyLeavePermission => EarlyLeavePermission;
}

/// <summary>Per-day result of the month-ordered status walk.</summary>
public sealed record MonthlyStatusOutcome(
    string Status,
    int DailyLateMinutes,
    int GraceConsumedMinutes,
    int LateHoursConsumed,
    bool EarlyLeaveConsumed = false);

public sealed class PermissionTracker
{
    public int GraceMinutesUsed { get; private set; }

    public int LateHoursUsed { get; private set; }

    public int EarlyLeavesUsed { get; private set; }

    public int LateHoursRemaining => AttendanceStatusRules.LatePermissionMonthlyHours - LateHoursUsed;

    public bool TryConsumeGraceLate(int minutes)
    {
        if (minutes <= 0)
            return true;
        if (GraceMinutesUsed + minutes > AttendanceStatusRules.GraceLateAllowanceMinutes)
            return false;
        GraceMinutesUsed += minutes;
        return true;
    }

    public bool TryConsumeLateHours(int hours, bool consume = true)
    {
        if (hours <= 0)
            return true;
        if (LateHoursUsed + hours > AttendanceStatusRules.LatePermissionMonthlyHours)
            return false;
        if (consume)
            LateHoursUsed += hours;
        return true;
    }

    public bool TryConsumeLate(int minutes)
    {
        if (minutes <= 0)
            return true;
        return TryConsumeLateHours((minutes + 59) / 60);
    }

    public bool TryConsumeEarlyLeave(TimeSpan checkoutTime)
    {
        if (checkoutTime < AttendanceStatusRules.EarlyLeavePermissionStart)
            return false;
        if (EarlyLeavesUsed >= AttendanceStatusRules.EarlyLeavePermissionMonthlyCount)
            return false;
        EarlyLeavesUsed++;
        return true;
    }
}
