using AttendanceApp.Data;
using AttendanceApp.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AttendanceApp.Services;

public class ZkAttendanceService
{
    private readonly string _pgConnectionString;
    private readonly IDbContextFactory<AppDbContext> _factory;

    public ZkAttendanceService(string pgConnectionString, IDbContextFactory<AppDbContext> factory)
    {
        _pgConnectionString = pgConnectionString;
        _factory = factory;
    }

    public async Task<SyncResult> SyncNewPunchesAsync(long lastId, int batchSize)
    {
        await using var conn = new NpgsqlConnection(_pgConnectionString);
        await conn.OpenAsync();

        var sql = @"
            SELECT id, CAST(emp_code AS VARCHAR) AS emp_no, punch_time
            FROM public.iclock_transaction
            WHERE id > @lastId
            ORDER BY id
            LIMIT @batchSize";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@lastId", lastId);
        cmd.Parameters.AddWithValue("@batchSize", batchSize);

        var punchesByDay = new Dictionary<(string EmpNo, DateTime Day), (DateTime First, DateTime Last)>();
        long maxId = lastId;

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var id = reader.GetInt64(0);
            var empNo = reader.GetString(1);
            var utc = reader.GetDateTime(2);

            if (id > maxId) maxId = id;

            var egypt = utc.AddHours(2);
            var day = egypt.Date;
            var key = (empNo, day);

            if (punchesByDay.TryGetValue(key, out var existing))
            {
                punchesByDay[key] = (existing.First < utc ? existing.First : utc,
                                     existing.Last > utc ? existing.Last : utc);
            }
            else
            {
                punchesByDay[key] = (utc, utc);
            }
        }

        if (punchesByDay.Count == 0)
            return new SyncResult { ProcessedCount = 0, MaxId = maxId };

        var processed = await UpsertPunchesAsync(punchesByDay, mergeWithExistingPunches: true);
        return new SyncResult { ProcessedCount = processed, MaxId = maxId };
    }

    public async Task<SyncResult> BackfillAllAsync()
    {
        await using var conn = new NpgsqlConnection(_pgConnectionString);
        await conn.OpenAsync();

        var sql = @"
            SELECT id, CAST(emp_code AS VARCHAR) AS emp_no, punch_time
            FROM public.iclock_transaction
            ORDER BY id";

        await using var cmd = new NpgsqlCommand(sql, conn);

        var punchesByDay = new Dictionary<(string EmpNo, DateTime Day), (DateTime First, DateTime Last)>();
        long maxId = 0;

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var id = reader.GetInt64(0);
            var empNo = reader.GetString(1);
            var utc = reader.GetDateTime(2);

            if (id > maxId) maxId = id;

            var egypt = utc.AddHours(2);
            var day = egypt.Date;
            var key = (empNo, day);

            if (punchesByDay.TryGetValue(key, out var existing))
            {
                punchesByDay[key] = (existing.First < utc ? existing.First : utc,
                                     existing.Last > utc ? existing.Last : utc);
            }
            else
            {
                punchesByDay[key] = (utc, utc);
            }
        }

        if (punchesByDay.Count == 0)
            return new SyncResult { ProcessedCount = 0, MaxId = maxId };

        var processed = await UpsertPunchesAsync(punchesByDay, mergeWithExistingPunches: false);
        return new SyncResult { ProcessedCount = processed, MaxId = maxId };
    }

    private async Task<int> UpsertPunchesAsync(
        Dictionary<(string EmpNo, DateTime Day), (DateTime First, DateTime Last)> punchesByDay,
        bool mergeWithExistingPunches)
    {
        const int chunkSize = 1000;

        using var db = await _factory.CreateDbContextAsync();
        db.ChangeTracker.AutoDetectChangesEnabled = false;

        var empDict = await db.Employees
            .Where(e => e.IsActive)
            .ToDictionaryAsync(e => e.FinancialNo);
        var rules = await db.ScheduleRules.Where(r => r.IsActive).ToListAsync();
        var calendarSettings = await db.AttendanceDaySettings.AsNoTracking().ToListAsync();

        var processed = 0;
        foreach (var chunk in punchesByDay.Where(p => empDict.ContainsKey(p.Key.EmpNo)).Chunk(chunkSize))
        {
            var empNos = chunk.Select(p => p.Key.EmpNo).Distinct().ToList();
            var dates = chunk.Select(p => p.Key.Day).Distinct().ToList();

            var existingRows = await db.DailyAttendances
                .Where(a => empNos.Contains(a.EmployeeFinancialNo) && dates.Contains(a.Date))
                .ToListAsync();

            var existingDict = existingRows.ToDictionary(a => (a.EmployeeFinancialNo, a.Date));

            foreach (var punch in chunk)
            {
                var empNo = punch.Key.EmpNo;
                var date = punch.Key.Day;
                var emp = empDict[empNo];
                var firstUtc = punch.Value.First;
                var lastUtc = punch.Value.Last;

                existingDict.TryGetValue((empNo, date), out var existing);

                if (mergeWithExistingPunches && existing != null)
                {
                    if (existing.FirstPunch.HasValue && existing.FirstPunch.Value < firstUtc)
                        firstUtc = existing.FirstPunch.Value;

                    if (existing.LastPunch.HasValue && existing.LastPunch.Value > lastUtc)
                        lastUtc = existing.LastPunch.Value;
                }

                var schedule = GetSchedule(emp, rules);
                var status = AttendanceStatusRules.Calculate(
                    firstUtc,
                    lastUtc,
                    schedule?.StartTime,
                    schedule?.EndTime,
                    date,
                    calendarSettings);

                if (existing == null)
                {
                    db.DailyAttendances.Add(new DailyAttendance
                    {
                        EmployeeFinancialNo = empNo,
                        Date = date,
                        FirstPunch = firstUtc,
                        LastPunch = lastUtc,
                        ScheduledStart = schedule?.StartTime,
                        ScheduledEnd = schedule?.EndTime,
                        Status = status
                    });
                }
                else
                {
                    existing.FirstPunch = firstUtc;
                    existing.LastPunch = lastUtc;
                    existing.ScheduledStart = schedule?.StartTime;
                    existing.ScheduledEnd = schedule?.EndTime;
                    existing.Status = status;
                }

                processed++;
            }

            db.ChangeTracker.DetectChanges();
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
        }

        db.ChangeTracker.AutoDetectChangesEnabled = true;
        return processed;
    }

    public async Task<string> PopulateDailyAttendanceRangeAsync(DateTime fromDate, DateTime toDate)
    {
        var startUtc = fromDate.ToUniversalTime();
        var endUtc = toDate.AddDays(1).ToUniversalTime();

        await using var conn = new NpgsqlConnection(_pgConnectionString);
        await conn.OpenAsync();

        var sql = @"
            SELECT CAST(emp_code AS VARCHAR) AS emp_no, punch_time
            FROM public.iclock_transaction
            WHERE punch_time >= @start AND punch_time < @end
            ORDER BY emp_no, punch_time";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@start", startUtc);
        cmd.Parameters.AddWithValue("@end", endUtc);

        var punchesByDay = new Dictionary<(string EmpNo, DateTime Day), (DateTime First, DateTime Last)>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var empNo = reader.GetString(0);
            var utc = reader.GetDateTime(1);
            var egypt = utc.AddHours(2);
            var day = egypt.Date;
            var key = (empNo, day);

            if (punchesByDay.TryGetValue(key, out var existing))
            {
                punchesByDay[key] = (existing.First < utc ? existing.First : utc,
                                     existing.Last > utc ? existing.Last : utc);
            }
            else
            {
                punchesByDay[key] = (utc, utc);
            }
        }

        using var db = await _factory.CreateDbContextAsync();
        var employees = await db.Employees.Where(e => e.IsActive).ToListAsync();
        var empDict = employees.ToDictionary(e => e.FinancialNo);
        var rules = await db.ScheduleRules.Where(r => r.IsActive).ToListAsync();
        var calendarSettings = await db.AttendanceDaySettings.AsNoTracking().ToListAsync();
        var normalizedFrom = fromDate.Date;
        var normalizedTo = toDate.Date;
        var existingRows = await db.DailyAttendances
            .Where(a => a.Date >= normalizedFrom && a.Date <= normalizedTo)
            .ToListAsync();
        var existingByEmployeeDate = existingRows
            .ToDictionary(a => (a.EmployeeFinancialNo, a.Date));

        var processed = 0;
        for (var date = normalizedFrom; date <= normalizedTo; date = date.AddDays(1))
        {
            foreach (var emp in employees)
            {
                var key = (emp.FinancialNo, date);
                DateTime? firstUtc = null;
                DateTime? lastUtc = null;

                if (punchesByDay.TryGetValue(key, out var p))
                {
                    firstUtc = p.First;
                    lastUtc = p.Last;
                }

                var schedule = GetSchedule(emp, rules);
                var status = AttendanceStatusRules.Calculate(
                    firstUtc,
                    lastUtc,
                    schedule?.StartTime,
                    schedule?.EndTime,
                    date,
                    calendarSettings);

                existingByEmployeeDate.TryGetValue((emp.FinancialNo, date), out var existing);

                if (existing == null)
                {
                    existing = new DailyAttendance
                    {
                        EmployeeFinancialNo = emp.FinancialNo,
                        Date = date,
                        FirstPunch = firstUtc,
                        LastPunch = lastUtc,
                        ScheduledStart = schedule?.StartTime,
                        ScheduledEnd = schedule?.EndTime,
                        Status = status
                    };
                    db.DailyAttendances.Add(existing);
                    existingByEmployeeDate[(emp.FinancialNo, date)] = existing;
                }
                else
                {
                    existing.FirstPunch = firstUtc;
                    existing.LastPunch = lastUtc;
                    existing.ScheduledStart = schedule?.StartTime;
                    existing.ScheduledEnd = schedule?.EndTime;
                    existing.Status = status;
                }

                processed++;
            }
        }

        await db.SaveChangesAsync();
        return $"Processed {processed} records from {fromDate:yyyy-MM-dd} to {toDate:yyyy-MM-dd}";
    }

    private static ScheduleRule? GetSchedule(Employee employee, List<ScheduleRule> rules)
    {
        return AttendanceStatusRules.ResolveSchedule(employee, rules);
    }

}

public class SyncResult
{
    public int ProcessedCount { get; set; }
    public long MaxId { get; set; }
}
