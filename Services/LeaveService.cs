using AttendanceApp.Data;
using AttendanceApp.Models;
using ClosedXML.Excel;
using ExcelDataReader;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace AttendanceApp.Services;

public class LeaveService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public LeaveService(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public async Task<LeaveTransaction> GrantLeaveAsync(string financialNo, int leaveTypeId, DateTime fromDate, DateTime toDate, double days, string? reason)
    {
        using var db = await _factory.CreateDbContextAsync();

        fromDate = fromDate.Date;
        toDate = toDate.Date;
        if (toDate < fromDate)
            throw new Exception("Leave end date cannot be before start date");

        days = (toDate - fromDate).TotalDays + 1;

        var emp = await db.Employees.FindAsync(financialNo)
            ?? throw new Exception($"Employee {financialNo} not found");

        var leaveType = await db.LeaveTypes.FindAsync(leaveTypeId)
            ?? throw new Exception($"Leave type {leaveTypeId} not found");

        var transaction = new LeaveTransaction
        {
            EmployeeFinancialNo = financialNo,
            LeaveTypeId = leaveTypeId,
            FromDate = fromDate,
            ToDate = toDate,
            DaysCount = days,
            Reason = reason,
            Status = "Approved",
            CreatedAt = DateTime.Now
        };

        db.LeaveTransactions.Add(transaction);

        if (!string.IsNullOrWhiteSpace(leaveType.BalanceType))
        {
            var balance = await db.LeaveBalances
                .FirstOrDefaultAsync(b => b.EmployeeFinancialNo == financialNo && b.Year == DateTime.Now.Year);

            if (balance != null)
            {
                switch (leaveType.BalanceType)
                {
                    case "Regular":
                        if (balance.RegularLeave >= days)
                            balance.RegularLeave -= days;
                        break;
                    case "Casual":
                        if (balance.CasualLeave >= days)
                            balance.CasualLeave -= days;
                        break;
                    case "Rest":
                        if (balance.RestAllowance >= days)
                            balance.RestAllowance -= days;
                        break;
                    case "Holiday":
                        if (balance.HolidayAllowance >= days)
                            balance.HolidayAllowance -= days;
                        break;
                    case "Sick":
                        break;
                }
            }
        }

        for (var d = fromDate; d <= toDate; d = d.AddDays(1))
        {
            var attendance = await db.DailyAttendances
                .FirstOrDefaultAsync(a => a.EmployeeFinancialNo == financialNo && a.Date == d);

            if (attendance == null)
            {
                db.DailyAttendances.Add(new DailyAttendance
                {
                    EmployeeFinancialNo = financialNo,
                    Date = d,
                    Status = "Leave",
                    LeaveTypeId = leaveTypeId,
                    Notes = reason
                });
            }
            else
            {
                attendance.Status = "Leave";
                attendance.LeaveTypeId = leaveTypeId;
                attendance.Notes = reason;
            }
        }

        await db.SaveChangesAsync();
        return transaction;
    }

    public async Task<List<LeaveTransaction>> GetLeaveTransactionsAsync(string? financialNo, int? leaveTypeId = null, DateTime? fromDate = null, DateTime? toDate = null)
    {
        using var db = await _factory.CreateDbContextAsync();

        var query = db.LeaveTransactions
            .Include(t => t.LeaveType)
            .Include(t => t.Employee)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(financialNo))
            query = query.Where(t => t.EmployeeFinancialNo == financialNo);

        if (leaveTypeId.HasValue)
            query = query.Where(t => t.LeaveTypeId == leaveTypeId.Value);

        if (fromDate.HasValue)
            query = query.Where(t => t.FromDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(t => t.ToDate <= toDate.Value);

        return await query
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<LeaveType>> GetLeaveTypesAsync()
    {
        using var db = await _factory.CreateDbContextAsync();
        return await db.LeaveTypes.ToListAsync();
    }

    public async Task<byte[]> GenerateLeaveUploadTemplateAsync()
    {
        using var db = await _factory.CreateDbContextAsync();
        var leaveTypes = await db.LeaveTypes.OrderBy(t => t.Code).ToListAsync();

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Leave Upload");
        var lookups = workbook.Worksheets.Add("Leave Types");

        var headers = new[] { "FinancialNo", "LeaveTypeCode", "FromDate", "ToDate", "Reason" };
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9EAF7");
        }

        sheet.Cell(2, 1).Value = "4779";
        sheet.Cell(2, 2).Value = leaveTypes.FirstOrDefault()?.Code ?? "A";
        sheet.Cell(2, 3).Value = DateTime.Today;
        sheet.Cell(2, 4).Value = DateTime.Today;
        sheet.Cell(2, 5).Value = "Example reason";
        sheet.Range("C:D").Style.DateFormat.Format = "yyyy-mm-dd";
        sheet.Columns().AdjustToContents();

        lookups.Cell(1, 1).Value = "Code";
        lookups.Cell(1, 2).Value = "Arabic Name";
        lookups.Cell(1, 3).Value = "English Name";
        lookups.Range("A1:C1").Style.Font.Bold = true;

        for (var i = 0; i < leaveTypes.Count; i++)
        {
            var row = i + 2;
            lookups.Cell(row, 1).Value = leaveTypes[i].Code;
            lookups.Cell(row, 2).Value = leaveTypes[i].NameAr ?? string.Empty;
            lookups.Cell(row, 3).Value = leaveTypes[i].NameEn ?? string.Empty;
        }

        lookups.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<string> ImportLeaveUploadAsync(string filePath)
    {
        using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true }
        });

        if (dataSet.Tables.Count == 0)
            return "No sheets found in leave upload file";

        using var db = await _factory.CreateDbContextAsync();
        var leaveTypes = await db.LeaveTypes.ToListAsync();
        var count = 0;

        foreach (System.Data.DataRow row in dataSet.Tables[0].Rows)
        {
            var financialNo = GetText(row, "FinancialNo", "الرقم المالي", "رقم مالي");
            var typeCode = GetText(row, "LeaveTypeCode", "LeaveType", "نوع الإجازة", "كود الإجازة");
            var reason = GetText(row, "Reason", "السبب");
            var fromDate = GetDate(row, "FromDate", "من تاريخ", "من");
            var toDate = GetDate(row, "ToDate", "إلى تاريخ", "الى تاريخ", "إلى");

            if (string.IsNullOrWhiteSpace(financialNo) || string.IsNullOrWhiteSpace(typeCode) || !fromDate.HasValue || !toDate.HasValue)
                continue;

            var leaveType = leaveTypes.FirstOrDefault(t => string.Equals(t.Code, typeCode, StringComparison.OrdinalIgnoreCase)
                || string.Equals(t.NameEn, typeCode, StringComparison.OrdinalIgnoreCase)
                || string.Equals(t.NameAr, typeCode, StringComparison.OrdinalIgnoreCase));
            if (leaveType == null)
                continue;

            await GrantLeaveAsync(financialNo, leaveType.Id, fromDate.Value, toDate.Value, 0, reason);
            count++;
        }

        return $"Imported {count} leave transactions";
    }

    private static string GetText(System.Data.DataRow row, params string[] names)
    {
        foreach (var name in names)
        {
            if (row.Table.Columns.Contains(name))
                return row[name]?.ToString()?.Trim() ?? string.Empty;
        }

        return string.Empty;
    }

    private static DateTime? GetDate(System.Data.DataRow row, params string[] names)
    {
        var text = GetText(row, names);
        if (string.IsNullOrWhiteSpace(text))
            return null;

        if (DateTime.TryParse(text, out var result))
            return result.Date;

        return null;
    }
}
