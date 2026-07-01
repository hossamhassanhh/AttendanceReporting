using AttendanceApp.Data;
using AttendanceApp.Models;
using ExcelDataReader;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace AttendanceApp.Services;

public class ExcelImportService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly ILogger<ExcelImportService> _logger;

    public ExcelImportService(IDbContextFactory<AppDbContext> factory, ILogger<ExcelImportService> logger)
    {
        _factory = factory;
        _logger = logger;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public async Task<string> ImportAllAsync()
    {
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var results = new List<string>();

        results.Add(await ImportEmployeesLevelsAsync(Path.Combine(docs, "الرئيسى مستويات.xlsx")));
        results.Add(await ImportLeaveBalancesAsync(Path.Combine(docs, "رصيد 2026.xls")));
        results.Add(await ImportMonthlyAttendanceAsync(Path.Combine(docs, "Copy of SAYED.xlsx")));

        return string.Join("\n", results);
    }

    public async Task<string> ImportEmployeesLevelsAsync(string filePath)
    {
        using var db = await _factory.CreateDbContextAsync();
        var count = 0;

        using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read);
        using var reader = ExcelReaderFactory.CreateOpenXmlReader(stream);
        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = false }
        });

        var levelMap = new Dictionary<int, string>
        {
            { 0, "Level 3" },
            { 1, "Level 2" },
            { 2, "Level 1" },
            { 3, "Top Management" },
            { 4, "Ladies" }
        };

        for (int s = 0; s < dataSet.Tables.Count && s < 5; s++)
        {
            var table = dataSet.Tables[s];
            var level = levelMap[s];

            for (int r = 2; r < table.Rows.Count; r++)
            {
                var row = table.Rows[r];
                var finNo = row[1]?.ToString()?.Trim() ?? string.Empty;
                var name = row[2]?.ToString()?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(finNo) || string.IsNullOrWhiteSpace(name)) continue;

                var jobTitle = row[3]?.ToString()?.Trim();
                var dept = row[5]?.ToString()?.Trim();

                var emp = await db.Employees.FindAsync(finNo);
                if (emp == null)
                {
                    db.Employees.Add(new Employee
                    {
                        FinancialNo = finNo,
                        Name = name,
                        JobTitle = jobTitle,
                        Department = dept,
                        Level = level
                    });
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(jobTitle)) emp.JobTitle = jobTitle;
                    if (!string.IsNullOrWhiteSpace(dept)) emp.Department = dept;
                    if (!string.IsNullOrWhiteSpace(level)) emp.Level = level;
                }
                count++;
            }
        }

        await db.SaveChangesAsync();
        return $"Imported {count} employees from الرئيسى مستويات.xlsx";
    }

    public async Task<string> ImportLeaveBalancesAsync(string filePath)
    {
        using var db = await _factory.CreateDbContextAsync();
        var count = 0;

        using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read);
        using var reader = ExcelReaderFactory.CreateBinaryReader(stream);
        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = false }
        });

        var table = dataSet.Tables[0];

        for (int r = 1; r < table.Rows.Count; r++)
        {
            var row = table.Rows[r];
            var finNo = row[0]?.ToString()?.Trim() ?? string.Empty;
            var name = row[1]?.ToString()?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(finNo) || string.IsNullOrWhiteSpace(name)) continue;

            var regLeave = GetDouble(row[2]);
            var casualLeave = GetDouble(row[3]);
            var restAllow = GetDouble(row[4]);
            var holiAllow = GetDouble(row[5]);
            var workLoc = row[6]?.ToString()?.Trim();
            var jobStatus = row[7]?.ToString()?.Trim();

            var emp = await db.Employees.FindAsync(finNo);
            if (emp == null)
            {
                emp = new Employee
                {
                    FinancialNo = finNo,
                    Name = name,
                    WorkLocation = workLoc,
                    JobStatus = jobStatus
                };
                db.Employees.Add(emp);
            }
            else
            {
                emp.Name = name;
                emp.WorkLocation = string.IsNullOrWhiteSpace(emp.WorkLocation) ? workLoc : emp.WorkLocation;
                emp.JobStatus = string.IsNullOrWhiteSpace(emp.JobStatus) ? jobStatus : emp.JobStatus;
            }
            await db.SaveChangesAsync();

            var balance = await db.LeaveBalances
                .FirstOrDefaultAsync(b => b.EmployeeFinancialNo == finNo && b.Year == 2026);

            if (balance == null)
            {
                db.LeaveBalances.Add(new LeaveBalance
                {
                    EmployeeFinancialNo = finNo,
                    Year = 2026,
                    RegularLeave = regLeave,
                    CasualLeave = casualLeave,
                    RestAllowance = restAllow,
                    HolidayAllowance = holiAllow
                });
            }
            else
            {
                balance.RegularLeave = regLeave;
                balance.CasualLeave = casualLeave;
                balance.RestAllowance = restAllow;
                balance.HolidayAllowance = holiAllow;
            }

            count++;
        }

        await db.SaveChangesAsync();
        return $"Imported {count} balances from رصيد 2026.xls";
    }

    public async Task<string> ImportMonthlyAttendanceAsync(string filePath)
    {
        using var db = await _factory.CreateDbContextAsync();
        var count = 0;

        using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read);
        using var reader = ExcelReaderFactory.CreateOpenXmlReader(stream);
        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = false }
        });

        var table = dataSet.Tables[0];

        for (int r = 7; r < table.Rows.Count; r++)
        {
            var row = table.Rows[r];
            var finNo = row[0]?.ToString()?.Trim() ?? string.Empty;
            var name = row[1]?.ToString()?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(finNo) || string.IsNullOrWhiteSpace(name)) continue;

            var jobTitle = row[2]?.ToString()?.Trim();
            var level = row[46]?.ToString()?.Trim();
            var department = row[45]?.ToString()?.Trim();

            var emp = await db.Employees.FindAsync(finNo);
            if (emp == null)
            {
                emp = new Employee
                {
                    FinancialNo = finNo,
                    Name = name,
                    JobTitle = jobTitle,
                    Level = level,
                    Department = department
                };
                db.Employees.Add(emp);
                await db.SaveChangesAsync();
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(jobTitle)) emp.JobTitle = jobTitle;
                if (!string.IsNullOrWhiteSpace(level)) emp.Level = level;
                if (!string.IsNullOrWhiteSpace(department)) emp.Department = department;
            }

            for (int d = 0; d < 31; d++)
            {
                var col = 3 + d;
                if (col >= row.ItemArray.Length) break;
                var code = row[col]?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(code)) continue;

                var existing = await db.MonthlyAttendances
                    .FirstOrDefaultAsync(a => a.EmployeeFinancialNo == finNo && a.Year == 2026 && a.Month == 5 && a.Day == d + 1);

                if (existing == null)
                {
                    db.MonthlyAttendances.Add(new MonthlyAttendance
                    {
                        EmployeeFinancialNo = finNo,
                        Year = 2026,
                        Month = 5,
                        Day = d + 1,
                        Code = code
                    });
                }
                else
                {
                    existing.Code = code;
                }

                count++;
            }
        }

        await db.SaveChangesAsync();
        return $"Imported {count} attendance records from Copy of SAYED.xlsx";
    }

    private static double GetDouble(object? value)
    {
        if (value == null || value == DBNull.Value) return 0;
        if (value is double d) return d;
        if (value is int i) return i;
        if (double.TryParse(value?.ToString(), out var result)) return result;
        return 0;
    }
}
