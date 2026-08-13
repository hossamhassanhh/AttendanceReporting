using AttendanceApp.Models;

namespace AttendanceApp.Services;

public static class AttendanceCalendarRules
{
    public const string WeeklyRest = "Weekly Rest";
    public const string WorkFromHome = "Work From Home";
    public const string Holiday = "Holiday";

    public static readonly string[] DayTypes = { WeeklyRest, WorkFromHome, Holiday };

    public static string? GetDayStatus(DateTime date, IReadOnlyCollection<AttendanceDaySetting>? settings)
    {
        var configured = settings?.FirstOrDefault(s => s.Date.Date == date.Date);
        if (configured != null)
            return configured.DayType;

        if (date.DayOfWeek == DayOfWeek.Friday || date.DayOfWeek == DayOfWeek.Saturday)
            return WeeklyRest;

        if (date.DayOfWeek == DayOfWeek.Sunday)
            return WorkFromHome;

        return null;
    }

    public static string? GetMonthlyCode(DateTime date, IReadOnlyCollection<AttendanceDaySetting>? settings)
    {
        return GetDayStatus(date, settings) switch
        {
            WeeklyRest => "R",
            WorkFromHome => "WH",
            Holiday => "H",
            _ => null
        };
    }
}
