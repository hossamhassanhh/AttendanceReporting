using AttendanceApp.Data;
using AttendanceApp.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceApp.Services;

public class DatabaseService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public DatabaseService(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        using var db = await _factory.CreateDbContextAsync();
        await db.Database.EnsureCreatedAsync();

        if (!await db.ScheduleRules.AnyAsync())
        {
            db.ScheduleRules.AddRange(
                new ScheduleRule { LevelName = "Ladies", StartTime = "08:31", EndTime = "15:10" },
                new ScheduleRule { LevelName = "Top Management", StartTime = "08:31", EndTime = "15:20" },
                new ScheduleRule { LevelName = "Level 1", StartTime = "08:31", EndTime = "15:25" },
                new ScheduleRule { LevelName = "Level 2", StartTime = "08:31", EndTime = "15:25" },
                new ScheduleRule { LevelName = "Level 3", StartTime = "08:31", EndTime = "15:25" }
            );
        }

        if (!await db.LeaveTypes.AnyAsync())
        {
            db.LeaveTypes.AddRange(
                new LeaveType { Code = "A", NameAr = "إجازة اعتيادى", NameEn = "Regular Leave", BalanceType = "Regular" },
                new LeaveType { Code = "S", NameAr = "إجازة مرضى", NameEn = "Sick Leave", BalanceType = "Sick" },
                new LeaveType { Code = "C", NameAr = "إجازة عارضه", NameEn = "Casual Leave", BalanceType = "Casual" },
                new LeaveType { Code = "B", NameAr = "غياب", NameEn = "Absence", BalanceType = null },
                new LeaveType { Code = "E", NameAr = "بدل راحه", NameEn = "Rest Allowance", BalanceType = "Rest" },
                new LeaveType { Code = "H", NameAr = "عطلة", NameEn = "Holiday", BalanceType = "Holiday" },
                new LeaveType { Code = "DI", NameAr = "مأمورية خارجية", NameEn = "External Mission", BalanceType = null },
                new LeaveType { Code = "DX", NameAr = "مأمورية داخلية", NameEn = "Internal Mission", BalanceType = null },
                new LeaveType { Code = "T", NameAr = "دورة تدريب", NameEn = "Training", BalanceType = null },
                new LeaveType { Code = "R", NameAr = "حضور", NameEn = "Present", BalanceType = null },
                new LeaveType { Code = "W", NameAr = "راحة أسبوعية", NameEn = "Weekly Rest", BalanceType = null }
            );
        }

        await db.SaveChangesAsync();
    }

    public async Task EnsureSyncStateTableAsync()
    {
        using var db = await _factory.CreateDbContextAsync();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'SyncStates')
            BEGIN
                CREATE TABLE [SyncStates] (
                    [Id] int NOT NULL IDENTITY,
                    [Key] nvarchar(50) NOT NULL,
                    [LastProcessedTransactionId] bigint NOT NULL DEFAULT 0,
                    [LastSyncTime] datetime2 NULL,
                    CONSTRAINT [PK_SyncStates] PRIMARY KEY ([Id])
                );
                CREATE UNIQUE INDEX [IX_SyncStates_Key] ON [SyncStates] ([Key]);
            END";
        await cmd.ExecuteNonQueryAsync();
    }
}
