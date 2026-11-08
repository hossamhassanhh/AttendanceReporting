using AttendanceApp.Data;
using AttendanceApp.Models;
using AttendanceApp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var connArg = args.SkipWhile(a => a != "--connection").Skip(1).FirstOrDefault()
    ?? "Server=10.51.0.100;Database=AttendanceApp;Trusted_Connection=True;Encrypt=False;TrustServerCertificate=True;";

var services = new ServiceCollection();
services.AddDbContextFactory<AppDbContext>(opt => opt.UseSqlServer(connArg));
services.AddScoped<LeaveService>();
services.AddScoped<LeaveApprovalWorkflowService>();
var sp = services.BuildServiceProvider();

using var scope = sp.CreateScope();
var dbf = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
var leaveSvc = scope.ServiceProvider.GetRequiredService<LeaveService>();

// Pick a leave type and a test date range (far future weekend-adjacent to minimize impact; will clean up)
int leaveTypeId;
using (var db = await dbf.CreateDbContextAsync())
{
    var lt = await db.LeaveTypes.AsNoTracking().OrderBy(x => x.Id).FirstAsync();
    leaveTypeId = lt.Id;
    Console.WriteLine($"Using LeaveType: {lt.Id} {lt.Code} {lt.NameEn ?? lt.NameAr}");
}

var testFrom = new DateTime(2030, 1, 6); // a Sunday in future - adjust; service will validate actual leave days
var testTo = new DateTime(2030, 1, 6);
Console.WriteLine($"Test period: {testFrom:yyyy-MM-dd} (may fail if not a working day in calendar; will try a few dates)");

// Find a valid working day in 2030-01 for IT employee 1039
DateTime? chosen = null;
using (var db = await dbf.CreateDbContextAsync())
{
    var cal = await db.AttendanceDaySettings.AsNoTracking().ToListAsync();
    for (int d = 5; d <= 15; d++)
    {
        var dt = new DateTime(2030, 1, d);
        var st = AttendanceApp.Services.AttendanceCalendarRules.GetDayStatus(dt, cal);
        if (st != AttendanceApp.Services.AttendanceCalendarRules.WeeklyRest && st != AttendanceApp.Services.AttendanceCalendarRules.Holiday)
        { chosen = dt; break; }
    }
}
if (chosen == null) { Console.WriteLine("No working day found in test range"); return; }
testFrom = chosen.Value; testTo = chosen.Value;
Console.WriteLine($"Chosen test date: {testFrom:yyyy-MM-dd}");

// Clean up any pre-existing test transaction for this exact slot
using (var db = await dbf.CreateDbContextAsync())
{
    var existing = await db.LeaveTransactions.Where(t => t.EmployeeFinancialNo == "1039" && t.FromDate == testFrom && t.ToDate == testTo && t.LeaveTypeId == leaveTypeId).ToListAsync();
    if (existing.Any()) { db.LeaveTransactions.RemoveRange(existing); await db.SaveChangesAsync(); Console.WriteLine($"Cleaned {existing.Count} pre-existing test tx(s)"); }
}

Console.WriteLine("\n=== STEP 1: Employee 1039 submits leave request ===");
var req = await leaveSvc.RequestLeaveAsync("1039", leaveTypeId, testFrom, testTo, "IT workflow cycle test", "test-1039");
Print(req);

Console.WriteLine("\n=== STEP 2: Employee delegate (1087) approves ===");
req = await leaveSvc.ApproveAsDelegateAsync(req.Id, "1087");
Print(req);

Console.WriteLine("\n=== STEP 3: Manager delegate (1110) approves (manager/manager delegate step) ===");
req = await leaveSvc.ApproveAsManagerAsync(req.Id, "1110");
Print(req);

Console.WriteLine("\n=== STEP 4: Department HR (1182) approves ===");
req = await leaveSvc.ApproveAsHrAsync(req.Id, "1182");
Print(req);

Console.WriteLine("\n=== CYCLE COMPLETE ===");
Console.WriteLine($"Final status: {req.Status} (expected: Approved)");

// Cleanup: remove the test transaction and any attendance side-effects? DeleteLeaveAsync restores balance/attendance.
// But the request is now Approved; DeleteLeaveAsync should handle it. Let's try to clean up.
Console.WriteLine("\n=== CLEANUP: deleting test transaction ===");
try { await leaveSvc.DeleteLeaveAsync(req.Id); Console.WriteLine("Test transaction deleted."); }
catch (Exception ex) { Console.WriteLine($"Cleanup delete failed (manual cleanup may be needed): {ex.Message}"); }

static void Print(LeaveTransaction t)
{
    Console.WriteLine($"  TxId={t.Id} Status={t.Status} Emp={t.EmployeeFinancialNo} Delegate={t.DelegateFinancialNo} Mgr={t.ManagerFinancialNo} MgrDel={t.ManagerDelegateFinancialNo} AssignedHR={t.AssignedHrFinancialNo} DelAppr={t.DelegateApprovedAt:u} MgrAppr={t.ManagerApprovedAt:u} HrAppr={t.HrApprovedAt:u}");
}