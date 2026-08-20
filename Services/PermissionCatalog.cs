namespace AttendanceApp.Services;

public static class PermissionCatalog
{
    public static readonly string[] All =
    {
        "Attendance",
        "Employees",
        "Balances",
        "Leaves",
        "MonthlyReports",
        "Imports",
        "Exports",
        "ManageCalendar",
        "ManagePermissions",
        "CreateUsers",
        "SelfAttendance",
        "SelfLeave",
        "LeaveApproval",
        "LeaveHR",
        "AdSync"
    };

    public static string AdminPermissions => string.Join(',', All);

    public static string EmployeePermissions => string.Join(',', All.Where(p => p != "CreateUsers"));
}
