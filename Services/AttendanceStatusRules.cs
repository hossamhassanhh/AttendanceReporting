using AttendanceApp.Models;

namespace AttendanceApp.Services;

public static class AttendanceStatusRules
{
    public const int CheckOutGraceMinutes = 30;
    public const int MonthlyLateAllowanceMinutes = 120;
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
        "Absent"
    };

    public static string Calculate(
        DateTime? firstPunch,
        DateTime? lastPunch,
        string? scheduledStart,
        string? scheduledEnd,
        DateTime date,
        IReadOnlyCollection<AttendanceDaySetting>? calendarSettings,
        DateTime? now = null)
    {
        var configuredDayStatus = AttendanceCalendarRules.GetDayStatus(date, calendarSettings);
        if (configuredDayStatus != null)
            return configuredDayStatus;

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

    public static int GetLateMinutes(DailyAttendance attendance)
    {
        if (!attendance.FirstPunch.HasValue
            || !TimeSpan.TryParse(attendance.ScheduledStart, out var scheduledStart))
            return 0;

        var checkIn = AsEgyptTime(attendance.FirstPunch.Value);
        var scheduled = attendance.Date.Date.Add(scheduledStart);
        var lateMinutes = (checkIn - scheduled).TotalMinutes;
        return lateMinutes > 0 ? (int)Math.Ceiling(lateMinutes) : 0;
    }

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

public sealed class PermissionTracker
{
    public int LateMinutesUsed { get; private set; }

    public int EarlyLeavesUsed { get; private set; }

    public bool TryConsumeLate(int minutes)
    {
        if (minutes <= 0)
            return true;
        LateMinutesUsed += minutes;
        return LateMinutesUsed <= AttendanceStatusRules.MonthlyLateAllowanceMinutes;
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
