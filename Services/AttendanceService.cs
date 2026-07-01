using Npgsql;
using AttendanceApp.Models;

namespace AttendanceApp.Services;

public class AttendanceService
{
    private readonly string _connectionString;

    public AttendanceService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<List<AttendanceData>> GetAttendanceAsync(List<int> employeeCodes, string startDate, string endDate)
    {
        var results = new List<AttendanceData>();

        var startUtc = DateTimeOffset.Parse(startDate).ToUniversalTime();
        var endUtc = DateTimeOffset.Parse(endDate).ToUniversalTime();

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        foreach (var empCode in employeeCodes)
        {
            try
            {
                var sql = @"
                    SELECT CAST(emp_code AS VARCHAR) AS emp_no, punch_time
                    FROM public.iclock_transaction
                    WHERE CAST(emp_code AS INTEGER) = @empCode
                      AND punch_time >= @startDate
                      AND punch_time < @endDate
                    ORDER BY emp_no, punch_time";

                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@empCode", empCode);
                cmd.Parameters.AddWithValue("@startDate", startUtc);
                cmd.Parameters.AddWithValue("@endDate", endUtc);

                var punchesByDay = new Dictionary<DateTime, (DateTime first, DateTime last)>();

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var utc = reader.GetDateTime(1);
                    var egypt = utc.AddHours(2);
                    var day = egypt.Date;

                    if (punchesByDay.TryGetValue(day, out var existing))
                    {
                        punchesByDay[day] = (existing.first < utc ? existing.first : utc,
                                              existing.last > utc ? existing.last : utc);
                    }
                    else
                    {
                        punchesByDay[day] = (utc, utc);
                    }
                }

                foreach (var kv in punchesByDay.OrderBy(k => k.Key))
                {
                    results.Add(new AttendanceData
                    {
                        EmployeeCode = empCode,
                        Date = kv.Key.ToString("yyyy-MM-dd"),
                        FirstPunch = kv.Value.first,
                        LastPunch = kv.Value.last
                    });
                }
            }
            catch (Exception ex)
            {
                results.Add(new AttendanceData
                {
                    EmployeeCode = empCode,
                    Error = ex.Message
                });
            }
        }

        return results;
    }
}
