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

    public async Task<LeaveTransaction> GrantLeaveAsync(string financialNo, int leaveTypeId, DateTime fromDate, DateTime toDate, string? reason, string? enteredBy = null)
    {
        using var db = await _factory.CreateDbContextAsync();

        fromDate = fromDate.Date;
        toDate = toDate.Date;
        if (toDate < fromDate)
            throw new Exception("Leave end date cannot be before start date");

        var calendarSettings = await db.AttendanceDaySettings.AsNoTracking().ToListAsync();
        var days = CountActualLeaveDays(fromDate, toDate, calendarSettings);
        if (days <= 0)
            throw new Exception("Selected range does not include actual leave days");

        var emp = await db.Employees.FindAsync(financialNo)
            ?? throw new Exception($"Employee {financialNo} not found");

        var leaveType = await db.LeaveTypes.FindAsync(leaveTypeId)
            ?? throw new Exception($"Leave type {leaveTypeId} not found");

        var duplicate = await db.LeaveTransactions.AnyAsync(item =>
            item.EmployeeFinancialNo == financialNo
            && item.LeaveTypeId == leaveTypeId
            && item.FromDate == fromDate
            && item.ToDate == toDate);
        if (duplicate)
            throw new InvalidDataException("A leave record already exists for this employee, leave type, and period.");

        var transaction = new LeaveTransaction
        {
            EmployeeFinancialNo = financialNo,
            LeaveTypeId = leaveTypeId,
            FromDate = fromDate,
            ToDate = toDate,
            DaysCount = days,
            Reason = reason,
            Status = "Approved",
            EnteredBy = string.IsNullOrWhiteSpace(enteredBy) ? "admin" : enteredBy.Trim(),
            CreatedAt = DateTime.Now
        };

        db.LeaveTransactions.Add(transaction);

        await AdjustBalanceAsync(db, financialNo, leaveType, fromDate, toDate, calendarSettings, -1);
        await ApplyLeaveAttendanceAsync(db, financialNo, leaveTypeId, fromDate, toDate, reason, calendarSettings);

        await db.SaveChangesAsync();
        return transaction;
    }

    public async Task<LeaveTransaction> UpdateLeaveAsync(
        int id, int leaveTypeId, DateTime fromDate, DateTime toDate, string? reason)
    {
        fromDate = fromDate.Date;
        toDate = toDate.Date;
        if (toDate < fromDate)
            throw new Exception("Leave end date cannot be before start date");

        using var db = await _factory.CreateDbContextAsync();
        await using var dbTransaction = await db.Database.BeginTransactionAsync();
        var transaction = await db.LeaveTransactions
            .Include(item => item.LeaveType)
            .FirstOrDefaultAsync(item => item.Id == id)
            ?? throw new KeyNotFoundException("Leave transaction was not found");
        var newLeaveType = await db.LeaveTypes.FindAsync(leaveTypeId)
            ?? throw new KeyNotFoundException("Leave type was not found");
        var duplicate = await db.LeaveTransactions.AnyAsync(item =>
            item.Id != id
            && item.EmployeeFinancialNo == transaction.EmployeeFinancialNo
            && item.LeaveTypeId == leaveTypeId
            && item.FromDate == fromDate
            && item.ToDate == toDate);
        if (duplicate)
            throw new InvalidDataException("A leave record already exists for this employee, leave type, and period.");
        var calendarSettings = await db.AttendanceDaySettings.AsNoTracking().ToListAsync();
        var days = CountActualLeaveDays(fromDate, toDate, calendarSettings);
        if (days <= 0)
            throw new Exception("Selected range does not include working days");

        if (transaction.LeaveType != null)
        {
            await AdjustBalanceAsync(
                db, transaction.EmployeeFinancialNo, transaction.LeaveType,
                transaction.FromDate, transaction.ToDate, calendarSettings, 1);
        }
        await ClearLeaveAttendanceAsync(
            db, transaction.EmployeeFinancialNo, transaction.LeaveTypeId,
            transaction.FromDate, transaction.ToDate);

        transaction.LeaveTypeId = leaveTypeId;
        transaction.FromDate = fromDate;
        transaction.ToDate = toDate;
        transaction.DaysCount = days;
        transaction.Reason = reason;

        await AdjustBalanceAsync(
            db, transaction.EmployeeFinancialNo, newLeaveType,
            fromDate, toDate, calendarSettings, -1);
        await ApplyLeaveAttendanceAsync(
            db, transaction.EmployeeFinancialNo, leaveTypeId,
            fromDate, toDate, reason, calendarSettings);
        await db.SaveChangesAsync();
        await dbTransaction.CommitAsync();
        return transaction;
    }

    public async Task DeleteLeaveAsync(int id)
    {
        using var db = await _factory.CreateDbContextAsync();
        await using var dbTransaction = await db.Database.BeginTransactionAsync();
        var transaction = await db.LeaveTransactions
            .Include(item => item.LeaveType)
            .FirstOrDefaultAsync(item => item.Id == id)
            ?? throw new KeyNotFoundException("Leave transaction was not found");
        var calendarSettings = await db.AttendanceDaySettings.AsNoTracking().ToListAsync();

        if (transaction.LeaveType != null)
        {
            await AdjustBalanceAsync(
                db, transaction.EmployeeFinancialNo, transaction.LeaveType,
                transaction.FromDate, transaction.ToDate, calendarSettings, 1);
        }
        await ClearLeaveAttendanceAsync(
            db, transaction.EmployeeFinancialNo, transaction.LeaveTypeId,
            transaction.FromDate, transaction.ToDate);
        db.LeaveTransactions.Remove(transaction);
        await db.SaveChangesAsync();
        await dbTransaction.CommitAsync();
    }

    public async Task<List<LeaveTransaction>> GetLeaveTransactionsAsync(string? financialNo, int? leaveTypeId = null, DateTime? fromDate = null, DateTime? toDate = null)
    {
        using var db = await _factory.CreateDbContextAsync();

        var query = db.LeaveTransactions
            .AsNoTracking()
            .Include(t => t.LeaveType)
            .Include(t => t.Employee)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(financialNo))
        {
            var term = financialNo.Trim();
            query = query.Where(t =>
                t.EmployeeFinancialNo.Contains(term)
                || (t.Employee != null && t.Employee.Name.Contains(term)));
        }

        if (leaveTypeId.HasValue)
            query = query.Where(t => t.LeaveTypeId == leaveTypeId.Value);

        if (fromDate.HasValue)
            query = query.Where(t => t.ToDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(t => t.FromDate <= toDate.Value);

        return await query
            .OrderByDescending(t => t.FromDate)
            .ThenByDescending(t => t.Id)
            .ToListAsync();
    }

    public async Task<List<LeaveLogDay>> GetLeaveLogDaysAsync(string? financialNo, int? leaveTypeId = null, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var transactions = await GetLeaveTransactionsAsync(financialNo, leaveTypeId, fromDate, toDate);
        using var db = await _factory.CreateDbContextAsync();
        var users = await db.AppUsers.AsNoTracking().ToDictionaryAsync(u => u.Username);
        var result = new List<LeaveLogDay>();

        foreach (var transaction in transactions)
        {
            users.TryGetValue(transaction.EnteredBy, out var enteredByUser);
            var enteredByAr = IsReadableText(enteredByUser?.DisplayNameAr)
                ? enteredByUser!.DisplayNameAr!
                : enteredByUser?.DisplayName ?? transaction.EnteredBy;

            result.Add(new LeaveLogDay
            {
                TransactionId = transaction.Id,
                EmployeeFinancialNo = transaction.EmployeeFinancialNo,
                EmployeeName = transaction.Employee?.Name ?? string.Empty,
                LeaveTypeId = transaction.LeaveTypeId,
                LeaveTypeNameAr = transaction.LeaveType?.NameAr ?? string.Empty,
                LeaveTypeNameEn = transaction.LeaveType?.NameEn ?? string.Empty,
                LeaveTypeCode = transaction.LeaveType?.Code ?? string.Empty,
                Date = transaction.FromDate,
                FromDate = transaction.FromDate,
                ToDate = transaction.ToDate,
                DaysCount = transaction.DaysCount,
                Reason = transaction.Reason,
                Status = transaction.Status,
                EnteredBy = enteredByUser?.DisplayName ?? transaction.EnteredBy,
                EnteredByAr = enteredByAr,
                EnteredByEn = enteredByUser?.DisplayNameEn ?? enteredByUser?.DisplayName ?? transaction.EnteredBy,
                CreatedAt = transaction.CreatedAt
            });
        }

        return result
            .OrderByDescending(d => d.FromDate)
            .ThenByDescending(d => d.TransactionId)
            .ToList();
    }

    public async Task<double> CalculateActualLeaveDaysAsync(DateTime fromDate, DateTime toDate)
    {
        using var db = await _factory.CreateDbContextAsync();
        var calendarSettings = await db.AttendanceDaySettings.AsNoTracking().ToListAsync();
        return CountActualLeaveDays(fromDate.Date, toDate.Date, calendarSettings);
    }

    private static double CountActualLeaveDays(DateTime fromDate, DateTime toDate, IReadOnlyCollection<AttendanceDaySetting> calendarSettings)
    {
        if (toDate < fromDate)
            return 0;

        var days = 0;
        for (var date = fromDate.Date; date <= toDate.Date; date = date.AddDays(1))
        {
            if (IsActualLeaveDay(date, calendarSettings))
                days++;
        }

        return days;
    }

    private static bool IsActualLeaveDay(DateTime date, IReadOnlyCollection<AttendanceDaySetting> calendarSettings)
    {
        var status = AttendanceCalendarRules.GetDayStatus(date, calendarSettings);
        return status != AttendanceCalendarRules.WeeklyRest
            && status != AttendanceCalendarRules.Holiday;
    }

    private static async Task AdjustBalanceAsync(
        AppDbContext db,
        string financialNo,
        LeaveType leaveType,
        DateTime fromDate,
        DateTime toDate,
        IReadOnlyCollection<AttendanceDaySetting> calendarSettings,
        int direction)
    {
        if (string.IsNullOrWhiteSpace(leaveType.BalanceType) || leaveType.BalanceType == "Sick")
            return;

        var daysByYear = Enumerable.Range(0, (toDate.Date - fromDate.Date).Days + 1)
            .Select(offset => fromDate.Date.AddDays(offset))
            .Where(date => IsActualLeaveDay(date, calendarSettings))
            .GroupBy(date => date.Year)
            .ToDictionary(group => group.Key, group => (double)group.Count());
        var balances = await db.LeaveBalances
            .Where(item => item.EmployeeFinancialNo == financialNo && daysByYear.Keys.Contains(item.Year))
            .ToDictionaryAsync(item => item.Year);

        foreach (var item in daysByYear)
        {
            if (!balances.TryGetValue(item.Key, out var balance))
                continue;
            var adjustment = direction * item.Value;
            switch (leaveType.BalanceType)
            {
                case "Regular": balance.RegularLeave += adjustment; break;
                case "Casual": balance.CasualLeave += adjustment; break;
                case "Rest": balance.RestAllowance += adjustment; break;
                case "Holiday": balance.HolidayAllowance += adjustment; break;
            }
        }
    }

    private static async Task ApplyLeaveAttendanceAsync(
        AppDbContext db,
        string financialNo,
        int leaveTypeId,
        DateTime fromDate,
        DateTime toDate,
        string? reason,
        IReadOnlyCollection<AttendanceDaySetting> calendarSettings)
    {
        var existing = await db.DailyAttendances
            .Where(item => item.EmployeeFinancialNo == financialNo
                && item.Date >= fromDate.Date
                && item.Date <= toDate.Date)
            .ToDictionaryAsync(item => item.Date.Date);
        for (var date = fromDate.Date; date <= toDate.Date; date = date.AddDays(1))
        {
            if (!IsActualLeaveDay(date, calendarSettings))
                continue;
            if (!existing.TryGetValue(date, out var attendance))
            {
                attendance = new DailyAttendance
                {
                    EmployeeFinancialNo = financialNo,
                    Date = date
                };
                db.DailyAttendances.Add(attendance);
            }
            attendance.Status = "Leave";
            attendance.LeaveTypeId = leaveTypeId;
            attendance.Notes = reason;
        }
    }

    private static async Task ClearLeaveAttendanceAsync(
        AppDbContext db,
        string financialNo,
        int leaveTypeId,
        DateTime fromDate,
        DateTime toDate)
    {
        var records = await db.DailyAttendances
            .Where(item => item.EmployeeFinancialNo == financialNo
                && item.Date >= fromDate.Date
                && item.Date <= toDate.Date
                && item.Status == "Leave"
                && item.LeaveTypeId == leaveTypeId)
            .ToListAsync();
        foreach (var attendance in records)
        {
            if (!attendance.FirstPunch.HasValue
                && !attendance.LastPunch.HasValue
                && string.IsNullOrWhiteSpace(attendance.ScheduledStart)
                && string.IsNullOrWhiteSpace(attendance.ScheduledEnd))
            {
                db.DailyAttendances.Remove(attendance);
                continue;
            }
            attendance.Status = attendance.FirstPunch.HasValue ? "Present" : "Unknown";
            attendance.LeaveTypeId = null;
            attendance.Notes = null;
        }
    }

    public async Task<List<LeaveType>> GetLeaveTypesAsync()
    {
        using var db = await _factory.CreateDbContextAsync();
        return await db.LeaveTypes.ToListAsync();
    }

    public async Task<byte[]> GenerateLeaveUploadTemplateAsync(bool isArabic)
    {
        using var db = await _factory.CreateDbContextAsync();
        var leaveTypes = await db.LeaveTypes.OrderBy(t => t.Code).ToListAsync();

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(isArabic ? "استيراد الإجازات" : "Leave Upload");
        var lookups = workbook.Worksheets.Add(isArabic ? "أنواع الإجازات" : "Leave Types");
        sheet.RightToLeft = isArabic;
        lookups.RightToLeft = isArabic;

        var headers = isArabic
            ? new[] { "الرقم المالي", "كود الإجازة", "من تاريخ", "إلى تاريخ", "السبب" }
            : new[] { "FinancialNo", "LeaveTypeCode", "FromDate", "ToDate", "Reason" };
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9EAF7");
        }

        sheet.Range("C2:D1000").Style.DateFormat.Format = "yyyy-mm-dd";
        sheet.Columns().AdjustToContents();

        var sample = workbook.Worksheets.Add(isArabic ? "مثال" : "Sample");
        sample.RightToLeft = isArabic;
        sample.Cell(1, 1).Value = isArabic
            ? "للاسترشاد فقط - أدخل البيانات في ورقة استيراد الإجازات"
            : "For guidance only - enter data in the Leave Upload worksheet";
        sample.Range(1, 1, 1, headers.Length).Merge();
        sample.Cell(1, 1).Style.Font.Bold = true;
        sample.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF2CC");
        for (var i = 0; i < headers.Length; i++)
        {
            sample.Cell(2, i + 1).Value = headers[i];
            sample.Cell(2, i + 1).Style.Font.Bold = true;
            sample.Cell(2, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#D9EAF7");
        }
        sample.Cell(3, 1).Value = "1001";
        sample.Cell(3, 2).Value = leaveTypes.FirstOrDefault()?.Code ?? "A";
        sample.Cell(3, 3).Value = DateTime.Today;
        sample.Cell(3, 4).Value = DateTime.Today;
        sample.Cell(3, 5).Value = isArabic ? "سبب الإجازة" : "Example reason";
        sample.Range("C3:D3").Style.DateFormat.Format = "yyyy-mm-dd";
        sample.Columns().AdjustToContents();

        lookups.Cell(1, 1).Value = isArabic ? "الكود" : "Code";
        lookups.Cell(1, 2).Value = isArabic ? "الاسم العربي" : "Arabic Name";
        lookups.Cell(1, 3).Value = isArabic ? "الاسم الإنجليزي" : "English Name";
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

    public async Task<string> ImportLeaveUploadAsync(string filePath, string enteredBy)
    {
        if (string.IsNullOrWhiteSpace(enteredBy))
            throw new ArgumentException("The authenticated username is required", nameof(enteredBy));

        using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true }
        });

        if (dataSet.Tables.Count == 0)
            return "No sheets found in leave upload file";

        var uploadTable = dataSet.Tables[0];
        var enteredByHeaders = new[] { "EnteredBy", "Entered By", "مدخل الإجازة", "مدخل الاجازة" };
        if (enteredByHeaders.Any(header => uploadTable.Columns.Contains(header)))
            throw new InvalidDataException("EnteredBy must not be included in the workbook; it is assigned automatically.");

        using var db = await _factory.CreateDbContextAsync();
        var leaveTypes = await db.LeaveTypes.ToListAsync();
        var count = 0;

        foreach (System.Data.DataRow row in uploadTable.Rows)
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

            await GrantLeaveAsync(financialNo, leaveType.Id, fromDate.Value, toDate.Value, reason, enteredBy);
            count++;
        }

        if (count == 0)
            throw new InvalidDataException("The leave workbook contains no valid leave rows.");

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

    private static bool IsReadableText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var text = value.Trim();
        return text.Any(character => character != '?') && !text.Contains('\uFFFD');
    }
}

public class LeaveLogDay
{
    public int TransactionId { get; set; }
    public string EmployeeFinancialNo { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public int LeaveTypeId { get; set; }
    public string LeaveTypeNameAr { get; set; } = string.Empty;
    public string LeaveTypeNameEn { get; set; } = string.Empty;
    public string LeaveTypeCode { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public double DaysCount { get; set; }
    public string? Reason { get; set; }
    public string Status { get; set; } = string.Empty;
    public string EnteredBy { get; set; } = string.Empty;
    public string EnteredByAr { get; set; } = string.Empty;
    public string EnteredByEn { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
