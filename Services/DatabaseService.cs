using AttendanceApp.Data;
using AttendanceApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AttendanceApp.Services;

public class DatabaseService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IPasswordHasher<AppUser> _passwordHasher;
    private readonly IConfiguration _configuration;
    private static readonly (string Username, string NameEn, string NameAr)[] HrEmployeeUsers =
    {
        ("fawzia.ibrahim", "Fawzia Ibrahim", "فوزية إبراهيم"),
        ("sayed.shibob", "Sayed Shibob", "سيد شيبوب"),
        ("amr.kasem", "Amr Kasem", "عمرو قاسم"),
        ("amr.ali", "Amr Ali", "عمرو علي"),
        ("alex", "Alex User", "مستخدم الإسكندرية"),
        ("vessels", "Vessels User", "مستخدم السفن")
    };

    public DatabaseService(
        IDbContextFactory<AppDbContext> factory,
        IPasswordHasher<AppUser> passwordHasher,
        IConfiguration configuration)
    {
        _factory = factory;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
    }

    public async Task InitializeAsync()
    {
        using var db = await _factory.CreateDbContextAsync();
        await db.Database.EnsureCreatedAsync();

        await db.Database.ExecuteSqlRawAsync(@"
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Employees' AND COLUMN_NAME = 'ScheduleStart')
                ALTER TABLE [Employees] ADD [ScheduleStart] nvarchar(10) NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Employees' AND COLUMN_NAME = 'ScheduleEnd')
                ALTER TABLE [Employees] ADD [ScheduleEnd] nvarchar(10) NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Employees' AND COLUMN_NAME = 'ManagerFinancialNo')
                ALTER TABLE [Employees] ADD [ManagerFinancialNo] nvarchar(20) NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Employees' AND COLUMN_NAME = 'BirthDate')
                ALTER TABLE [Employees] ADD [BirthDate] date NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Employees' AND COLUMN_NAME = 'HireDate')
                ALTER TABLE [Employees] ADD [HireDate] date NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Employees' AND COLUMN_NAME = 'ContractType')
                ALTER TABLE [Employees] ADD [ContractType] nvarchar(100) NULL;");

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
                new LeaveType { Code = "A", NameAr = "إجازة اعتيادية", NameEn = "Regular Leave", BalanceType = "Regular" },
                new LeaveType { Code = "S", NameAr = "إجازة مرضية", NameEn = "Sick Leave", BalanceType = "Sick" },
                new LeaveType { Code = "C", NameAr = "إجازة عارضة", NameEn = "Casual Leave", BalanceType = "Casual" },
                new LeaveType { Code = "B", NameAr = "غياب", NameEn = "Absence", BalanceType = null },
                new LeaveType { Code = "E", NameAr = "بدل راحة", NameEn = "Rest Allowance", BalanceType = "Rest" },
                new LeaveType { Code = "H", NameAr = "عطلة", NameEn = "Holiday", BalanceType = "Holiday" },
                new LeaveType { Code = "DI", NameAr = "مأمورية خارجية", NameEn = "External Mission", BalanceType = null },
                new LeaveType { Code = "DX", NameAr = "مأمورية داخلية", NameEn = "Internal Mission", BalanceType = null },
                new LeaveType { Code = "T", NameAr = "دورة تدريب", NameEn = "Training", BalanceType = null },
                new LeaveType { Code = "R", NameAr = "حضور", NameEn = "Present", BalanceType = null },
                new LeaveType { Code = "W", NameAr = "راحة أسبوعية", NameEn = "Weekly Rest", BalanceType = null }
            );
        }

        if (!await db.LeaveTypes.AnyAsync(type => type.Code == "P"))
        {
            db.LeaveTypes.Add(new LeaveType
            {
                Code = "P",
                NameAr = "إذن",
                NameEn = "Permission",
                BalanceType = null
            });
        }
        {
            var improvedArabicNames = new Dictionary<string, string>
            {
                ["A"] = "إجازة اعتيادية",
                ["S"] = "إجازة مرضية",
                ["C"] = "إجازة عارضة",
                ["E"] = "بدل راحة"
            };

            var leaveTypes = await db.LeaveTypes
                .Where(t => improvedArabicNames.Keys.Contains(t.Code))
                .ToListAsync();

            foreach (var leaveType in leaveTypes)
                leaveType.NameAr = improvedArabicNames[leaveType.Code];
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

        cmd.CommandText = @"
            IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'LeaveTransactions')
               AND NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'LeaveTransactions' AND COLUMN_NAME = 'EnteredBy')
            BEGIN
                ALTER TABLE [LeaveTransactions] ADD [EnteredBy] nvarchar(80) NOT NULL CONSTRAINT [DF_LeaveTransactions_EnteredBy] DEFAULT 'admin';
            END";
        await cmd.ExecuteNonQueryAsync();

        cmd.CommandText = @"
            IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'LeaveTransactions')
               AND NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'LeaveTransactions' AND COLUMN_NAME = 'ManagerFinancialNo')
            BEGIN
                ALTER TABLE [LeaveTransactions] ADD [ManagerFinancialNo] nvarchar(20) NULL;
            END

            IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'LeaveTransactions')
               AND NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'LeaveTransactions' AND COLUMN_NAME = 'ManagerApprovedAt')
            BEGIN
                ALTER TABLE [LeaveTransactions] ADD [ManagerApprovedAt] datetime2 NULL;
            END

            IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'LeaveTransactions')
               AND NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'LeaveTransactions' AND COLUMN_NAME = 'HrApprovedAt')
            BEGIN
                ALTER TABLE [LeaveTransactions] ADD [HrApprovedAt] datetime2 NULL;
            END

            IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'LeaveTransactions')
               AND NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'LeaveTransactions' AND COLUMN_NAME = 'RejectedBy')
            BEGIN
                ALTER TABLE [LeaveTransactions] ADD [RejectedBy] nvarchar(80) NULL;
            END

            IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'LeaveTransactions')
               AND NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'LeaveTransactions' AND COLUMN_NAME = 'RejectedAt')
            BEGIN
                ALTER TABLE [LeaveTransactions] ADD [RejectedAt] datetime2 NULL;
            END

            IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'LeaveTransactions')
               AND NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'LeaveTransactions' AND COLUMN_NAME = 'RejectionReason')
            BEGIN
                ALTER TABLE [LeaveTransactions] ADD [RejectionReason] nvarchar(500) NULL;
            END";
        await cmd.ExecuteNonQueryAsync();

        cmd.CommandText = @"
            UPDATE [LeaveTransactions]
            SET [ManagerFinancialNo] = e.[ManagerFinancialNo]
            FROM [LeaveTransactions] AS lt
            INNER JOIN [Employees] AS e ON e.[FinancialNo] = lt.[EmployeeFinancialNo]
            WHERE lt.[Status] = 'PendingManager'
              AND (lt.[ManagerFinancialNo] IS NULL OR LTRIM(RTRIM(lt.[ManagerFinancialNo])) = '')
              AND e.[ManagerFinancialNo] IS NOT NULL
              AND LTRIM(RTRIM(e.[ManagerFinancialNo])) <> '';";
        await cmd.ExecuteNonQueryAsync();

        cmd.CommandText = @"
            UPDATE [LeaveTransactions]
            SET [EnteredBy] = 'admin'
            WHERE [EnteredBy] IS NULL OR LTRIM(RTRIM([EnteredBy])) = '';";
        await cmd.ExecuteNonQueryAsync();

        cmd.CommandText = @"
            IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AppUsers')
            BEGIN
                CREATE TABLE [AppUsers] (
                    [Id] int NOT NULL IDENTITY,
                    [Username] nvarchar(80) NOT NULL,
                    [DisplayName] nvarchar(120) NOT NULL,
                    [DisplayNameAr] nvarchar(120) NULL,
                    [DisplayNameEn] nvarchar(120) NULL,
                    [Role] nvarchar(30) NOT NULL,
                    [Permissions] nvarchar(1000) NOT NULL,
                    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
                    [CreatedAt] datetime2 NOT NULL DEFAULT SYSDATETIME(),
                    CONSTRAINT [PK_AppUsers] PRIMARY KEY ([Id])
                );
                CREATE UNIQUE INDEX [IX_AppUsers_Username] ON [AppUsers] ([Username]);
            END

            IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AppUsers')
               AND NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'AppUsers' AND COLUMN_NAME = 'DisplayNameAr')
            BEGIN
                ALTER TABLE [AppUsers] ADD [DisplayNameAr] nvarchar(120) NULL;
            END

            IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AppUsers')
               AND NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'AppUsers' AND COLUMN_NAME = 'DisplayNameEn')
            BEGIN
                ALTER TABLE [AppUsers] ADD [DisplayNameEn] nvarchar(120) NULL;
            END

            IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AppUsers')
               AND NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'AppUsers' AND COLUMN_NAME = 'PasswordHash')
            BEGIN
                ALTER TABLE [AppUsers] ADD [PasswordHash] nvarchar(500) NOT NULL
                    CONSTRAINT [DF_AppUsers_PasswordHash] DEFAULT '';
            END

            IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AppUsers')
               AND NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'AppUsers' AND COLUMN_NAME = 'MustChangePassword')
            BEGIN
                ALTER TABLE [AppUsers] ADD [MustChangePassword] bit NOT NULL
                    CONSTRAINT [DF_AppUsers_MustChangePassword] DEFAULT CAST(1 AS bit);
            END

            IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AppUsers')
               AND NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'AppUsers' AND COLUMN_NAME = 'LastLoginAt')
            BEGIN
                ALTER TABLE [AppUsers] ADD [LastLoginAt] datetime2 NULL;
            END

            IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AppUsers')
               AND NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'AppUsers' AND COLUMN_NAME = 'SessionVersion')
            BEGIN
                ALTER TABLE [AppUsers] ADD [SessionVersion] int NOT NULL
                    CONSTRAINT [DF_AppUsers_SessionVersion] DEFAULT 0;
            END

            IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AttendanceDaySettings')
            BEGIN
                CREATE TABLE [AttendanceDaySettings] (
                    [Id] int NOT NULL IDENTITY,
                    [Date] datetime2 NOT NULL,
                    [DayType] nvarchar(30) NOT NULL,
                    [Notes] nvarchar(300) NULL,
                    [CreatedAt] datetime2 NOT NULL DEFAULT SYSDATETIME(),
                    CONSTRAINT [PK_AttendanceDaySettings] PRIMARY KEY ([Id])
                );
                CREATE UNIQUE INDEX [IX_AttendanceDaySettings_Date] ON [AttendanceDaySettings] ([Date]);
            END";
        await cmd.ExecuteNonQueryAsync();

        cmd.CommandText = @"
            IF NOT EXISTS (SELECT 1 FROM [AppUsers] WHERE [Username] = 'admin')
            BEGIN
                INSERT INTO [AppUsers] ([Username], [DisplayName], [Role], [Permissions], [IsActive], [CreatedAt])
                VALUES ('admin', 'Administrator', 'Admin', @permissions, CAST(1 AS bit), SYSDATETIME());
            END";
        var permissionsParam = cmd.CreateParameter();
        permissionsParam.ParameterName = "@permissions";
        permissionsParam.Value = PermissionCatalog.AdminPermissions;
        cmd.Parameters.Add(permissionsParam);
        await cmd.ExecuteNonQueryAsync();

        await SeedHrEmployeeUsersAsync(db);
        await RemoveDuplicatesAndEnforceUniqueIndexesAsync(db);
    }

    private static async Task RemoveDuplicatesAndEnforceUniqueIndexesAsync(AppDbContext db)
    {
        // Keep the newest record when historic data contains the same business key.
        // The unique indexes then prevent any future duplicate writes.
        await db.Database.ExecuteSqlRawAsync(@"
            SET XACT_ABORT ON;
            BEGIN TRANSACTION;

            ;WITH Duplicates AS (
                SELECT [Id], ROW_NUMBER() OVER (PARTITION BY [EmployeeFinancialNo], [Year] ORDER BY [Id] DESC) AS [RowNo]
                FROM [LeaveBalances]
            ) DELETE FROM Duplicates WHERE [RowNo] > 1;

            ;WITH Duplicates AS (
                SELECT [Id], ROW_NUMBER() OVER (PARTITION BY [EmployeeFinancialNo], [Year], [Month], [Day] ORDER BY [Id] DESC) AS [RowNo]
                FROM [MonthlyAttendances]
            ) DELETE FROM Duplicates WHERE [RowNo] > 1;

            ;WITH Duplicates AS (
                SELECT [Id], [EmployeeFinancialNo], [FromDate], [DaysCount], [LeaveTypeId],
                    ROW_NUMBER() OVER (PARTITION BY [EmployeeFinancialNo], [LeaveTypeId], [FromDate], [ToDate] ORDER BY [Id] DESC) AS [RowNo]
                FROM [LeaveTransactions]
            ), Restores AS (
                SELECT d.[EmployeeFinancialNo], YEAR(d.[FromDate]) AS [Year],
                    SUM(CASE WHEN t.[BalanceType] = 'Regular' THEN d.[DaysCount] ELSE 0 END) AS [RegularLeave],
                    SUM(CASE WHEN t.[BalanceType] = 'Casual' THEN d.[DaysCount] ELSE 0 END) AS [CasualLeave],
                    SUM(CASE WHEN t.[BalanceType] = 'Rest' THEN d.[DaysCount] ELSE 0 END) AS [RestAllowance],
                    SUM(CASE WHEN t.[BalanceType] = 'Holiday' THEN d.[DaysCount] ELSE 0 END) AS [HolidayAllowance]
                FROM Duplicates AS d
                INNER JOIN [LeaveTypes] AS t ON t.[Id] = d.[LeaveTypeId]
                WHERE d.[RowNo] > 1
                GROUP BY d.[EmployeeFinancialNo], YEAR(d.[FromDate])
            )
            UPDATE b SET
                b.[RegularLeave] = b.[RegularLeave] + r.[RegularLeave],
                b.[CasualLeave] = b.[CasualLeave] + r.[CasualLeave],
                b.[RestAllowance] = b.[RestAllowance] + r.[RestAllowance],
                b.[HolidayAllowance] = b.[HolidayAllowance] + r.[HolidayAllowance]
            FROM [LeaveBalances] AS b
            INNER JOIN Restores AS r
                ON r.[EmployeeFinancialNo] = b.[EmployeeFinancialNo]
                AND r.[Year] = b.[Year];

            ;WITH Duplicates AS (
                SELECT [Id], ROW_NUMBER() OVER (PARTITION BY [EmployeeFinancialNo], [LeaveTypeId], [FromDate], [ToDate] ORDER BY [Id] DESC) AS [RowNo]
                FROM [LeaveTransactions]
            ) DELETE FROM Duplicates WHERE [RowNo] > 1;

            ;WITH Duplicates AS (
                SELECT [Id], ROW_NUMBER() OVER (PARTITION BY [Username] ORDER BY [Id] DESC) AS [RowNo]
                FROM [AppUsers]
            ) DELETE FROM Duplicates WHERE [RowNo] > 1;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'UX_LeaveBalances_EmployeeFinancialNo_Year')
                CREATE UNIQUE INDEX [UX_LeaveBalances_EmployeeFinancialNo_Year] ON [LeaveBalances] ([EmployeeFinancialNo], [Year]);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'UX_MonthlyAttendances_EmployeeFinancialNo_Year_Month_Day')
                CREATE UNIQUE INDEX [UX_MonthlyAttendances_EmployeeFinancialNo_Year_Month_Day] ON [MonthlyAttendances] ([EmployeeFinancialNo], [Year], [Month], [Day]);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'UX_LeaveTransactions_EmployeeFinancialNo_LeaveTypeId_FromDate_ToDate')
                CREATE UNIQUE INDEX [UX_LeaveTransactions_EmployeeFinancialNo_LeaveTypeId_FromDate_ToDate] ON [LeaveTransactions] ([EmployeeFinancialNo], [LeaveTypeId], [FromDate], [ToDate]);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'UX_AppUsers_Username')
                CREATE UNIQUE INDEX [UX_AppUsers_Username] ON [AppUsers] ([Username]);

            COMMIT TRANSACTION;");
    }

    private async Task SeedHrEmployeeUsersAsync(AppDbContext db)
    {
        var usernames = HrEmployeeUsers.Select(u => u.Username).Append("admin").ToArray();
        var existingUsers = await db.AppUsers.Where(u => usernames.Contains(u.Username)).ToListAsync();

        var admin = existingUsers.FirstOrDefault(u => u.Username == "admin");
        if (admin != null)
        {
            admin.DisplayNameAr = "مدير النظام";
            admin.DisplayNameEn = string.IsNullOrWhiteSpace(admin.DisplayNameEn) ? admin.DisplayName : admin.DisplayNameEn;
            if (string.IsNullOrWhiteSpace(admin.PasswordHash))
            {
                var password = _configuration["Authentication:InitialAdminPassword"] ?? "Admin@2026!";
                admin.PasswordHash = _passwordHasher.HashPassword(admin, password);
                admin.MustChangePassword = true;
            }
        }

        foreach (var seed in HrEmployeeUsers)
        {
            var user = existingUsers.FirstOrDefault(u => u.Username == seed.Username);
            if (user == null)
            {
                user = new AppUser
                {
                    Username = seed.Username,
                    DisplayName = seed.NameEn,
                    DisplayNameAr = seed.NameAr,
                    DisplayNameEn = seed.NameEn,
                    Role = "Employee",
                    Permissions = PermissionCatalog.EmployeePermissions,
                    IsActive = true
                };
                var password = _configuration["Authentication:InitialEmployeePassword"] ?? "Welcome@2026!";
                user.PasswordHash = _passwordHasher.HashPassword(user, password);
                user.MustChangePassword = true;
                db.AppUsers.Add(user);
                continue;
            }

            user.DisplayName = seed.NameEn;
            user.DisplayNameAr = seed.NameAr;
            user.DisplayNameEn = seed.NameEn;
            user.Role = "Employee";
            user.Permissions = PermissionCatalog.EmployeePermissions;
            user.IsActive = true;
            if (string.IsNullOrWhiteSpace(user.PasswordHash))
            {
                var password = _configuration["Authentication:InitialEmployeePassword"] ?? "Welcome@2026!";
                user.PasswordHash = _passwordHasher.HashPassword(user, password);
                user.MustChangePassword = true;
            }
        }

        await db.SaveChangesAsync();
    }
}
