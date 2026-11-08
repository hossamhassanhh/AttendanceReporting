using AttendanceApp.Data;
using AttendanceApp.Services;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Usage: dotnet run --project Tools/WorkflowImportTest -- --connection "<conn>" [--generate-only <path>]
var connArg = args.SkipWhile(a => a != "--connection").Skip(1).FirstOrDefault()
    ?? "Server=10.51.0.100;Database=AttendanceApp;Trusted_Connection=True;Encrypt=False;TrustServerCertificate=True;";
var generateOnly = args.SkipWhile(a => a != "--generate-only").Skip(1).FirstOrDefault();

var services = new ServiceCollection();
services.AddDbContextFactory<AppDbContext>(opt => opt.UseSqlServer(connArg));
services.AddScoped<LeaveApprovalWorkflowService>();
var sp = services.BuildServiceProvider();

// 1) Generate IT department test workflow Excel
using var wb = new XLWorkbook();
var ws = wb.Worksheets.Add("Approval Workflow");
ws.Cell(1, 1).Value = "IT Dept approval cycle test: Employee -> Employee Delegate -> Manager/Manager Delegate -> HR";
ws.Range(1, 1, 1, 8).Merge();
string[] headers = { "Department", "EmployeeFinancialNo", "EmployeeName", "EmployeeDelegateFinancialNo", "DeptManagerFinancialNo", "DeptManagerDelegateFinancialNo", "DeptHrFinancialNo", "Notes" };
for (int i = 0; i < headers.Length; i++) { ws.Cell(2, i + 1).Value = headers[i]; ws.Cell(2, i + 1).Style.Font.Bold = true; }

// Resolve IT department name from employee 1039
using (var scope = sp.CreateScope())
{
    var dbf = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    using var db = await dbf.CreateDbContextAsync();
    var emp1039 = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.FinancialNo == "1039");
    var itDept = emp1039?.Department ?? "IT";
    Console.WriteLine($"IT Department (from 1039): {itDept}");

    // Rows: cover the cycle - 1039 is the employee who will submit; delegate 1087 approves; manager 693 or delegate 1110 approves; HR 1182 approves
    var rows = new[]
    {
        new[] { itDept, "1039", emp1039?.Name ?? "1039", "1087", "693", "1110", "1182", "Test: 1039 submits -> delegate 1087 -> manager 693/1110 -> HR 1182" },
        new[] { itDept, "1087", (await db.Employees.AsNoTracking().FirstOrDefaultAsync(e=>e.FinancialNo=="1087"))?.Name ?? "1087", "", "693", "1110", "1182", "1087 has no employee delegate" },
        new[] { itDept, "1110", (await db.Employees.AsNoTracking().FirstOrDefaultAsync(e=>e.FinancialNo=="1110"))?.Name ?? "1110", "", "693", "1110", "1182", "Manager delegate" },
        new[] { itDept, "1128", (await db.Employees.AsNoTracking().FirstOrDefaultAsync(e=>e.FinancialNo=="1128"))?.Name ?? "1128", "", "693", "1110", "1182", "IT staff" },
        new[] { itDept, "693",  (await db.Employees.AsNoTracking().FirstOrDefaultAsync(e=>e.FinancialNo=="693"))?.Name  ?? "693",  "", "693", "1110", "1182", "Manager" },
    };
    for (int r = 0; r < rows.Length; r++)
        for (int c = 0; c < rows[r].Length; c++)
            ws.Cell(r + 3, c + 1).Value = rows[r][c] ?? "";
}
ws.Columns().AdjustToContents();
for (int c = 1; c <= 8; c++) if (ws.Column(c).Width > 40) ws.Column(c).Width = 40;

var outPath = generateOnly ?? Path.Combine(AppContext.BaseDirectory, "IT-ApprovalWorkflow-Test.xlsx");
wb.SaveAs(outPath);
Console.WriteLine($"Excel saved: {outPath}");
if (generateOnly != null) { Console.WriteLine("Generate-only mode; skipping import."); return; }

// 2) Import via the bulk upload service (exercises the feature)
using (var scope = sp.CreateScope())
{
    var svc = scope.ServiceProvider.GetRequiredService<LeaveApprovalWorkflowService>();
    var result = await svc.ImportApprovalWorkflowAsync(outPath, "admin-test");
    Console.WriteLine($"Import result: {result}");
}

// 3) Verify
using (var scope = sp.CreateScope())
{
    var dbf = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    using var db = await dbf.CreateDbContextAsync();
    foreach (var fn in new[] { "1039", "1087", "693", "1110", "1182" })
    {
        var e = await db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.FinancialNo == fn);
        Console.WriteLine($"Emp {fn}: Delegate={e?.DelegateFinancialNo ?? "(null)"} Mgr={e?.ManagerFinancialNo ?? "(null)"} Dept={e?.Department}");
    }
    var emp1039 = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.FinancialNo == "1039");
    if (emp1039?.Department != null)
    {
        var cfg = await db.DepartmentWorkflowConfigs.AsNoTracking().FirstOrDefaultAsync(c => c.Department == emp1039.Department);
        Console.WriteLine($"DeptConfig [{cfg?.Department}]: Mgr={cfg?.ManagerFinancialNo} MgrDel={cfg?.ManagerDelegateFinancialNo} HR={cfg?.HrFinancialNo}");
    }
}
Console.WriteLine("Done.");