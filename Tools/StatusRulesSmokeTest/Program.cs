using AttendanceApp.Models;
using AttendanceApp.Services;

var date = new DateTime(2026, 7, 30);
var scheduleStart = "08:31";
var scheduleEnd = "15:25";

Require(
    AttendanceStatusRules.Calculate(
        Utc(date, 8, 24), Utc(date, 8, 24), scheduleStart, scheduleEnd, date, null, date.AddHours(10)) == "Checked In",
    "A single punch before shift finalization must be checked in, not absent.");

Require(
    AttendanceStatusRules.Calculate(
        Utc(date, 8, 24), Utc(date, 8, 24), scheduleStart, scheduleEnd, date, null, date.AddDays(1)) == "Missing Check Out",
    "A historical single punch must be missing checkout, not absent.");

Require(
    AttendanceStatusRules.Calculate(
        Utc(date, 8, 31), Utc(date, 10, 24), scheduleStart, scheduleEnd, date, null, date.AddDays(1)) == "Absent",
    "A completed shift shorter than half the required duration must remain absent.");

var holiday = new[]
{
    new AttendanceDaySetting { Date = date, DayType = AttendanceCalendarRules.Holiday }
};
Require(
    AttendanceStatusRules.Calculate(null, null, scheduleStart, scheduleEnd, date, holiday, date.AddDays(1)) == AttendanceCalendarRules.Holiday,
    "An official holiday must override automatic absence.");

Require(
    AttendanceStatusRules.Calculate(
        Utc(date, 8, 32), Utc(date, 15, 30), scheduleStart, scheduleEnd, date, null, date.AddDays(1)) == "Late",
    "A full shift starting one minute after start must be late without a grace period.");

Require(
    AttendanceStatusRules.Calculate(
        Utc(date, 8, 31), Utc(date, 15, 30), scheduleStart, scheduleEnd, date, null, date.AddDays(1)) == "Present",
    "A full shift starting exactly at start must be present, not late.");

Require(
    AttendanceStatusRules.Calculate(
        Utc(date, 8, 31), Utc(date, 15, 17), scheduleStart, scheduleEnd, date, null, date.AddDays(1)) == "Early Leave",
    "Checking out before the scheduled end must be early leave even when arriving on time.");

Require(
    AttendanceStatusRules.Calculate(
        Utc(date, 8, 43), Utc(date, 14, 33), scheduleStart, scheduleEnd, date, null, date.AddDays(1)) == "Early Leave",
    "A late arrival that also checks out early must be early leave, not late or present.");

Require(
    AttendanceStatusRules.Calculate(
        Utc(date, 8, 35), Utc(date, 15, 17), scheduleStart, "15:20", date, null, date.AddDays(1)) == "Early Leave",
    "An employee leaving at 15:17 with a 15:20 schedule end must be early leave.");

Require(
    AttendanceStatusRules.GetLateMinutes(new DailyAttendance
    {
        Date = date,
        ScheduledStart = scheduleStart,
        FirstPunch = Utc(date, 8, 31)
    }) == 0,
    "A punch exactly at start must consume no late minutes.");

Require(
    AttendanceStatusRules.GetLateMinutes(new DailyAttendance
    {
        Date = date,
        ScheduledStart = scheduleStart,
        FirstPunch = Utc(date, 8, 32)
    }) == 1,
    "A punch one minute after start must consume one late minute.");

Require(
    AttendanceStatusRules.GetLateMinutes(new DailyAttendance
    {
        Date = date,
        ScheduledStart = scheduleStart,
        FirstPunch = Utc(date, 8, 35)
    }) == 4,
    "A punch at 08:35 must consume four late minutes.");

var permissions = new PermissionTracker();
Require(permissions.TryConsumeGraceLate(10) && permissions.GraceMinutesUsed == 10, "Ten late minutes must fit the 30-minute grace.");
Require(permissions.TryConsumeGraceLate(20) && permissions.GraceMinutesUsed == 30, "Grace exactly at 30 minutes must still be covered.");
Require(!permissions.TryConsumeGraceLate(1) && permissions.GraceMinutesUsed == 30, "Grace over 30 minutes must fall through to permission.");
Require(permissions.TryConsumeLate(10) && permissions.LateHoursUsed == 1, "Ten late minutes must consume one hour.");
Require(permissions.TryConsumeLate(50) && permissions.LateHoursUsed == 2, "Late time within two whole hours must be covered.");
Require(!permissions.TryConsumeLate(1) && permissions.LateHoursUsed == 3, "Late time over the 2-hour quota must not be covered.");
Require(!permissions.TryConsumeEarlyLeave(new TimeSpan(9, 30, 0)) && permissions.EarlyLeavesUsed == 0, "Early leave before 10:00 must not consume a permission.");
Require(permissions.TryConsumeEarlyLeave(new TimeSpan(14, 0, 0)) && permissions.EarlyLeavesUsed == 1, "First early leave after 10:00 must consume a permission.");
Require(permissions.TryConsumeEarlyLeave(new TimeSpan(15, 0, 0)) && permissions.EarlyLeavesUsed == 2, "Second early leave after 10:00 must consume a permission.");
Require(!permissions.TryConsumeEarlyLeave(new TimeSpan(14, 30, 0)) && permissions.EarlyLeavesUsed == 2, "Third early leave must not consume a permission.");

// --- Monthly grace / late-permission quota rules on a single day ---

var quota = new MonthlyStatusQuota();
Require(
    AttendanceStatusRules.Calculate(
        Utc(date, 8, 35), Utc(date, 15, 30), scheduleStart, scheduleEnd, date, null, date.AddDays(1), quota) == "Present"
        && quota.GraceMinutesRemaining == 26,
    "A late arrival within the 30-minute grace must be Present and consume the grace.");

quota = new MonthlyStatusQuota();
Require(
    AttendanceStatusRules.Calculate(
        Utc(date, 9, 6), Utc(date, 15, 30), scheduleStart, scheduleEnd, date, null, date.AddDays(1), quota) == "Present"
        && quota.GraceMinutesRemaining == 30
        && quota.LateHoursRemaining == 1,
    "A late arrival beyond the grace must fall to the 2-hour quota.");

quota = new MonthlyStatusQuota();
quota.TryConsumeLateHours(2);
Require(
    AttendanceStatusRules.Calculate(
        Utc(date, 9, 6), Utc(date, 15, 30), scheduleStart, scheduleEnd, date, null, date.AddDays(1), quota) == "Late",
    "A late arrival with an exhausted 2-hour quota must stay Late.");

quota = new MonthlyStatusQuota();
Require(
    AttendanceStatusRules.Calculate(
        Utc(date, 9, 6), Utc(date, 15, 0), scheduleStart, scheduleEnd, date, null, date.AddDays(1), quota) == "Early Leave"
        && quota.GraceMinutesRemaining == 30
        && quota.LateHoursRemaining == 2,
    "A late arrival with an early check-out must stay Early Leave and consume nothing.");

quota = new MonthlyStatusQuota();
Require(
    AttendanceStatusRules.Calculate(
        Utc(date, 9, 6), Utc(date, 15, 0), scheduleStart, scheduleEnd, date, null, date.AddDays(1), quota, enteredLeave: true) == "Present"
        && quota.GraceMinutesRemaining == 30,
    "An early check-out with an entered leave/permission must become Present without consuming quotas.");

quota = new MonthlyStatusQuota();
Require(
    AttendanceStatusRules.Calculate(
        Utc(date, 9, 16), Utc(date, 15, 30), scheduleStart, scheduleEnd, date, null, date.AddDays(1), quota, latePermissionLeave: true) == "Present"
        && quota.GraceMinutesRemaining == 30
        && quota.LateHoursRemaining == 2,
    "An entered late permission (PL) must excuse the lateness without consuming quotas.");

quota = new MonthlyStatusQuota();
Require(
    AttendanceStatusRules.Calculate(
        Utc(date, 8, 41), Utc(date, 15, 30), scheduleStart, scheduleEnd, date, null, date.AddDays(1), quota, enteredLeave: true) == "Present"
        && quota.GraceMinutesRemaining == 30,
    "Days covered by an entered leave consume no grace quota.");

// --- Month-ordered walk: quotas are consumed chronologically ---

var oct5 = new DateTime(2026, 10, 5);
var monthRecords = new List<DailyAttendance>
{
    NewRecord(1, "E1", oct5, 8, 51, 15, 30, "Late"),
    NewRecord(2, "E1", oct5.AddDays(1), 8, 46, 15, 30, "Late"),
    NewRecord(3, "E1", oct5.AddDays(2), 8, 47, 15, 30, "Late"),
    NewRecord(4, "E1", oct5.AddDays(3), 8, 47, 15, 30, "Late"),
};
var walkOutcomes = AttendanceStatusRules.CalculateMonthlyStatuses(monthRecords, null, null, null);
Require(walkOutcomes[1].Status == "Present" && walkOutcomes[1].GraceConsumedMinutes == 20 && walkOutcomes[1].DailyLateMinutes == 20,
    "Day 1 (20 late minutes) must be Present from the grace quota.");
Require(walkOutcomes[2].Status == "Present" && walkOutcomes[2].GraceConsumedMinutes == 0 && walkOutcomes[2].LateHoursConsumed == 1 && walkOutcomes[2].DailyLateMinutes == 15,
    "Day 2 (15 late minutes, only 10 grace left) must consume the 2-hour quota.");
Require(walkOutcomes[3].Status == "Present" && walkOutcomes[3].LateHoursConsumed == 1,
    "Day 3 (16 late minutes) must still fit the remaining hour.");
Require(walkOutcomes[4].Status == "Late" && walkOutcomes[4].LateHoursConsumed == 0,
    "Day 4 with both quotas exhausted must stay Late.");

// --- Entered leave flags and permission leave types ---

var leaveTypeCodes = new Dictionary<int, string> { [1] = "A", [2] = "PL", [3] = "PE", [4] = "P" };
var leaveFlags = new Dictionary<(string EmployeeFinancialNo, DateTime Date), DayLeaveFlags>
{
    [("E2", oct5)] = new DayLeaveFlags(true, false)
};
var flagged = new List<DailyAttendance>
{
    NewRecord(10, "E2", oct5, 8, 41, 15, 30, "Late"),
};
var flaggedOutcomes = AttendanceStatusRules.CalculateMonthlyStatuses(flagged, leaveFlags, null, leaveTypeCodes);
Require(flaggedOutcomes[10].Status == "Present" && flaggedOutcomes[10].GraceConsumedMinutes == 0,
    "A late day covered by an entered leave must be Present and consume nothing.");

var permissionRecords = new List<DailyAttendance>
{
    NewRecord(20, "E3", oct5, 8, 41, 15, 30, "Late", leaveTypeId: 2),          // PL entered
    NewRecord(21, "E3", oct5.AddDays(1), 8, 46, 15, 30, "Late"),               // automatic
    NewRecord(22, "E4", oct5, 0, 0, 0, 0, "Leave", leaveTypeId: 1),            // regular leave, no punches
    NewRecord(23, "E5", oct5, 8, 0, 15, 30, "Leave", leaveTypeId: 4),          // P permission, full attendance
    NewRecord(24, "E6", oct5, 0, 0, 0, 0, "Leave", leaveTypeId: 3),            // PE permission, no punches
};
var permissionOutcomes = AttendanceStatusRules.CalculateMonthlyStatuses(permissionRecords, null, null, leaveTypeCodes);
Require(permissionOutcomes[20].Status == "Present" && permissionOutcomes[20].GraceConsumedMinutes == 0,
    "A PL (late permission) day with late punches must be Present and consume no grace.");
Require(permissionOutcomes[21].Status == "Present" && permissionOutcomes[21].GraceConsumedMinutes == 15,
    "The grace quota must still be full after the PL day.");
Require(!permissionOutcomes.ContainsKey(22),
    "Regular leave days must be skipped by the status walk.");
Require(permissionOutcomes[23].Status == "Present",
    "A permission day with full attendance must be Present.");
Require(permissionOutcomes[24].Status == "Leave",
    "A permission day without proof of attendance must stay Leave.");

Console.WriteLine("PASS attendance status rules");

static DailyAttendance NewRecord(
    int id,
    string employeeNo,
    DateTime date,
    int inHour,
    int inMinute,
    int outHour,
    int outMinute,
    string status,
    int? leaveTypeId = null)
{
    var hasPunches = inHour != 0 || outHour != 0;
    return new DailyAttendance
    {
        Id = id,
        EmployeeFinancialNo = employeeNo,
        Date = date,
        FirstPunch = hasPunches ? Utc(date, inHour, inMinute) : null,
        LastPunch = hasPunches ? Utc(date, outHour, outMinute) : null,
        ScheduledStart = "08:31",
        ScheduledEnd = "15:25",
        Status = status,
        LeaveTypeId = leaveTypeId
    };
}

static DateTime Utc(DateTime date, int hour, int minute) =>
    DateTime.SpecifyKind(date.Date.AddHours(hour - 2).AddMinutes(minute), DateTimeKind.Utc);

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
