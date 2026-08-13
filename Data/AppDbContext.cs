using Microsoft.EntityFrameworkCore;
using AttendanceApp.Models;

namespace AttendanceApp.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<ScheduleRule> ScheduleRules => Set<ScheduleRule>();
    public DbSet<LeaveBalance> LeaveBalances => Set<LeaveBalance>();
    public DbSet<MonthlyAttendance> MonthlyAttendances => Set<MonthlyAttendance>();
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<DailyAttendance> DailyAttendances => Set<DailyAttendance>();
    public DbSet<LeaveTransaction> LeaveTransactions => Set<LeaveTransaction>();
    public DbSet<SyncState> SyncStates => Set<SyncState>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<AttendanceDaySetting> AttendanceDaySettings => Set<AttendanceDaySetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Employee>(e =>
        {
            e.Property(x => x.FinancialNo).ValueGeneratedNever();
        });

        modelBuilder.Entity<LeaveBalance>(e =>
        {
            e.HasIndex(x => new { x.EmployeeFinancialNo, x.Year }).IsUnique();
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeFinancialNo);
        });

        modelBuilder.Entity<MonthlyAttendance>(e =>
        {
            e.HasIndex(x => new { x.EmployeeFinancialNo, x.Year, x.Month, x.Day }).IsUnique();
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeFinancialNo);
        });

        modelBuilder.Entity<DailyAttendance>(e =>
        {
            e.HasIndex(x => new { x.EmployeeFinancialNo, x.Date }).IsUnique();
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeFinancialNo);
            e.HasOne(x => x.LeaveType).WithMany().HasForeignKey(x => x.LeaveTypeId);
        });

        modelBuilder.Entity<LeaveTransaction>(e =>
        {
            e.HasIndex(x => x.EmployeeFinancialNo);
            e.HasIndex(x => new { x.EmployeeFinancialNo, x.LeaveTypeId, x.FromDate, x.ToDate }).IsUnique();
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeFinancialNo);
            e.HasOne(x => x.LeaveType).WithMany().HasForeignKey(x => x.LeaveTypeId);
        });

        modelBuilder.Entity<ScheduleRule>(e =>
        {
            e.HasIndex(x => x.LevelName).IsUnique();
        });

        modelBuilder.Entity<LeaveType>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<SyncState>(e =>
        {
            e.HasIndex(x => x.Key).IsUnique();
        });

        modelBuilder.Entity<AppUser>(e =>
        {
            e.HasIndex(x => x.Username).IsUnique();
        });

        modelBuilder.Entity<AttendanceDaySetting>(e =>
        {
            e.HasIndex(x => x.Date).IsUnique();
        });
    }
}
