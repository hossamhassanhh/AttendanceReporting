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
        results.Add(await ImportMonthlyAttendanceAsync(Path.Combine(docs, "Monthly Attendance Template.xlsx")));

        return string.Join("\n", results);
    }

    public async Task<string> ImportEmployeesLevelsAsync(string filePath)
    {
        using var db = await _factory.CreateDbContextAsync();
        var count = 0;

        using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read);
        using var reader = ExcelReaderFactory.CreateReader(stream);
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
            if (IsGuidanceSheet(table.TableName))
                continue;

            var level = levelMap[s];

            var headerRow = FindHeaderRow(table, "Financial No", "FinancialNo", "PR", "الرقم المالي");
            var firstDataRow = headerRow >= 0 ? headerRow + 1 : 2;
            var financialNoColumn = headerRow >= 0
                ? FindColumn(table, headerRow, "Financial No", "FinancialNo", "PR", "الرقم المالي")
                : 1;
            var nameColumn = headerRow >= 0
                ? FindColumn(table, headerRow, "Name", "الاسم")
                : 2;
            var jobTitleColumn = headerRow >= 0
                ? FindColumn(table, headerRow, "Job Title", "المسمى الوظيفي")
                : 3;
            var departmentColumn = headerRow >= 0
                ? FindColumn(table, headerRow, "Department", "الإدارة")
                : 5;
            var levelColumn = headerRow >= 0
                ? FindColumn(table, headerRow, "Level", "المستوى")
                : -1;

            for (int r = firstDataRow; r < table.Rows.Count; r++)
            {
                var row = table.Rows[r];
                var finNo = GetCellText(row, financialNoColumn);
                var name = GetCellText(row, nameColumn);
                if (string.IsNullOrWhiteSpace(finNo) || string.IsNullOrWhiteSpace(name)) continue;

                var jobTitle = GetCellText(row, jobTitleColumn);
                var dept = GetCellText(row, departmentColumn);
                var importedLevel = GetCellText(row, levelColumn);
                if (string.IsNullOrWhiteSpace(importedLevel))
                    importedLevel = level;

                var emp = await db.Employees.FindAsync(finNo);
                if (emp == null)
                {
                    db.Employees.Add(new Employee
                    {
                        FinancialNo = finNo,
                        Name = name,
                        JobTitle = jobTitle,
                        Department = dept,
                        Level = importedLevel
                    });
                }
                else
                {
                    emp.Name = name;
                    if (!string.IsNullOrWhiteSpace(jobTitle)) emp.JobTitle = jobTitle;
                    if (!string.IsNullOrWhiteSpace(dept)) emp.Department = dept;
                    if (!string.IsNullOrWhiteSpace(importedLevel)) emp.Level = importedLevel;
                }
                count++;
            }
        }

        if (count == 0)
            throw new InvalidDataException("The employee workbook contains no valid employee rows.");

        await db.SaveChangesAsync();
        return $"Imported {count} employees from الرئيسى مستويات.xlsx";
    }

    public async Task<string> ImportLeaveBalancesAsync(string filePath)
    {
        using var db = await _factory.CreateDbContextAsync();
        var count = 0;

        using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = false }
        });

        var table = dataSet.Tables[0];

        var headerRow = FindHeaderRow(table, "Financial No", "FinancialNo", "PR", "الرقم المالي");
        var firstDataRow = headerRow >= 0 ? headerRow + 1 : 1;
        var financialNoColumn = headerRow >= 0 ? FindColumn(table, headerRow, "Financial No", "FinancialNo", "PR", "الرقم المالي") : 0;
        var nameColumn = headerRow >= 0 ? FindColumn(table, headerRow, "Name", "الاسم") : 1;
        var regularColumn = headerRow >= 0 ? FindColumn(table, headerRow, "Regular Leave", "الإجازة الاعتيادية") : 2;
        var casualColumn = headerRow >= 0 ? FindColumn(table, headerRow, "Casual Leave", "الإجازة العارضة") : 3;
        var restColumn = headerRow >= 0 ? FindColumn(table, headerRow, "Rest Allowance", "رصيد الراحة") : 4;
        var holidayColumn = headerRow >= 0 ? FindColumn(table, headerRow, "Holiday Allowance", "رصيد العطلات") : 5;
        var locationColumn = headerRow >= 0 ? FindColumn(table, headerRow, "Work Location", "موقع العمل") : 6;
        var statusColumn = headerRow >= 0 ? FindColumn(table, headerRow, "Job Status", "الحالة الوظيفية") : 7;

        for (int r = firstDataRow; r < table.Rows.Count; r++)
        {
            var row = table.Rows[r];
            var finNo = GetCellText(row, financialNoColumn);
            var name = GetCellText(row, nameColumn);
            if (string.IsNullOrWhiteSpace(finNo) || string.IsNullOrWhiteSpace(name)) continue;

            var regLeave = GetDouble(GetCell(row, regularColumn));
            var casualLeave = GetDouble(GetCell(row, casualColumn));
            var restAllow = GetDouble(GetCell(row, restColumn));
            var holiAllow = GetDouble(GetCell(row, holidayColumn));
            var workLoc = GetCellText(row, locationColumn);
            var jobStatus = GetCellText(row, statusColumn);

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

        if (count == 0)
            throw new InvalidDataException("The balance workbook contains no valid balance rows.");

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

        if (count == 0)
            throw new InvalidDataException("The monthly attendance workbook contains no valid attendance values.");

        await db.SaveChangesAsync();
        return $"Imported {count} attendance records from Monthly Attendance Template.xlsx";
    }

    private static double GetDouble(object? value)
    {
        if (value == null || value == DBNull.Value) return 0;
        if (value is double d) return d;
        if (value is int i) return i;
        if (double.TryParse(value?.ToString(), out var result)) return result;
        return 0;
    }

    private static int FindHeaderRow(System.Data.DataTable table, params string[] names)
    {
        for (var row = 0; row < Math.Min(table.Rows.Count, 12); row++)
        {
            if (FindColumn(table, row, names) >= 0)
                return row;
        }

        return -1;
    }

    private static bool IsGuidanceSheet(string? sheetName)
    {
        return string.Equals(sheetName?.Trim(), "Sample", StringComparison.OrdinalIgnoreCase)
            || string.Equals(sheetName?.Trim(), "مثال", StringComparison.OrdinalIgnoreCase);
    }

    private static int FindColumn(System.Data.DataTable table, int headerRow, params string[] names)
    {
        if (headerRow < 0 || headerRow >= table.Rows.Count)
            return -1;

        for (var column = 0; column < table.Columns.Count; column++)
        {
            var value = table.Rows[headerRow][column]?.ToString()?.Trim();
            if (names.Any(name => string.Equals(value, name, StringComparison.OrdinalIgnoreCase)))
                return column;
        }

        return -1;
    }

    private static object? GetCell(System.Data.DataRow row, int column)
    {
        return column >= 0 && column < row.ItemArray.Length ? row[column] : null;
    }

    private static string GetCellText(System.Data.DataRow row, int column)
    {
        return GetCell(row, column)?.ToString()?.Trim() ?? string.Empty;
    }
}
