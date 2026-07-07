using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

var outputPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "AttendanceReporting-Business-Technical-Overview.pdf"));

Document.Create(container =>
{
    container.Page(page =>
    {
        page.Size(PageSizes.A4);
        page.Margin(36);
        page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

        page.Header().Column(col =>
        {
            col.Item().Text("Attendance Reporting Application")
                .FontSize(22).Bold().FontColor(Colors.Blue.Darken3);
            col.Item().Text("Business and Technical Overview")
                .FontSize(13).FontColor(Colors.Grey.Darken2);
            col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Blue.Lighten2);
        });

        page.Content().PaddingVertical(18).Column(col =>
        {
            Section(col, "Executive Summary", new[]
            {
                "The application centralizes employee attendance, leave management, and monthly attendance reporting for PMS operational teams.",
                "It synchronizes punch data from the ZK/Biotime PostgreSQL database into a SQL Server application database, applies schedule and leave rules, and produces browser previews plus Excel/PDF monthly reports.",
                "The Excel output is designed to preserve the official SAYED monthly timesheet format while dynamically reflecting selected year, month, department, attendance logs, and approved leave intervals."
            });

            Section(col, "Business Objectives", new[]
            {
                "Reduce manual preparation of monthly attendance sheets.",
                "Provide HR/administration with a single browser interface for attendance, employees, balances, leave grants, and monthly reports.",
                "Keep attendance data current through automatic ZK/Biotime polling instead of manual imports.",
                "Improve consistency of attendance status calculation, including present, late, early leave, absent, weekly rest, approved leave, missions, training, and Sunday work-from-home.",
                "Support department-filtered reporting and official Excel/PDF outputs for management review and employee signatures."
            });

            Section(col, "Primary Business Workflows", new[]
            {
                "Attendance review: users select employee numbers and a date range, then view daily attendance, punch times, duration, schedule, and status.",
                "Employee search: users search active employees by name or financial number and view job title, level, and department.",
                "Leave balance review: users inspect annual leave balances per employee.",
                "Leave granting: users select an employee, leave type, date interval, and reason; the system calculates the day count and updates daily attendance records.",
                "Leave audit: users can view leave transactions for one employee or all employees when no employee is selected.",
                "Monthly reporting: users choose year, month, and optional department, preview the SAYED-style table in the browser, then download Excel or PDF for PC use."
            });

            Section(col, "Key Business Rules", new[]
            {
                "Friday and Saturday are treated as weekly rest.",
                "Sunday is treated as work from home and exported as WH unless an approved leave interval overrides it.",
                "Approved leave intervals take priority in monthly reports and are mapped to official report codes such as A, S, C, E, DI, DX, and T.",
                "Early leave is detected when the last punch is earlier than the scheduled end time.",
                "Late arrival is detected when the first punch is later than the schedule start plus the grace period.",
                "If worked time is less than 50 percent of required scheduled time, the day is treated as absent."
            });

            Section(col, "Technical Architecture", new[]
            {
                "Backend: ASP.NET Core on .NET 10 with controller-based REST APIs.",
                "Frontend: static HTML, CSS, and JavaScript served from wwwroot with Arabic RTL UI support.",
                "Application database: SQL Server accessed through Entity Framework Core.",
                "Biometric source database: ZK/Biotime PostgreSQL accessed with Npgsql.",
                "Background service: hosted polling service reads new ZK transactions every configured interval and updates daily attendance.",
                "Reporting: QuestPDF generates PDF reports; Excel monthly export uses Microsoft Excel COM automation to preserve the SAYED template layout, drawings, and formatting."
            });

            Section(col, "Core Components", new[]
            {
                "ZkAttendanceService reads punch transactions, converts timestamps to Egypt-local business days, merges first and last punches, and calculates daily status.",
                "ZkSyncBackgroundService maintains continuous incremental synchronization using SyncStates.LastProcessedTransactionId.",
                "LeaveService manages leave grants, leave transactions, automatic day-count calculation, balance deduction, and leave upload template generation.",
                "ExportService builds monthly preview data, overlays approved leave intervals, applies Sunday WH logic, and generates Excel/PDF outputs.",
                "DatabaseService initializes required reference tables, schedule rules, leave types, and sync-state storage.",
                "TrackingController, ExportController, ImportController, LeaveController, EmployeesController, and AttendanceController expose the application APIs."
            });

            Section(col, "Data Model Summary", new[]
            {
                "Employee: financial number, name, job title, department, work location, job status, level, and active flag.",
                "DailyAttendance: employee, date, first punch, last punch, scheduled start/end, status, leave type, and notes.",
                "LeaveType: report code, Arabic/English names, and optional balance type.",
                "LeaveTransaction: employee, leave type, from/to dates, calculated days count, reason, approval status, and creation timestamp.",
                "LeaveBalance: annual balances for regular leave, casual leave, rest allowance, and holiday allowance.",
                "MonthlyAttendance: imported legacy or template attendance code by employee, year, month, and day.",
                "SyncState: incremental ZK synchronization watermark and last sync metadata."
            });

            Section(col, "Integrations and External Dependencies", new[]
            {
                "SQL Server Express hosts the application database.",
                "PostgreSQL/Biotime provides raw biometric punch transactions from public.iclock_transaction.",
                "Microsoft Excel must be installed on the Windows host for template-preserving monthly Excel export.",
                "The SAYED template is expected at C:\\Users\\4779\\Documents\\Copy of SAYED.xlsx.",
                "GitHub repository stores source code; live appsettings.json and database backup files are intentionally excluded unless explicitly handled separately."
            });

            Section(col, "Operational Notes", new[]
            {
                "The app can be run locally with dotnet run and served in a browser.",
                "ZK synchronization settings are configured through ZkSync.PollingIntervalSeconds and ZkSync.BatchSize.",
                "Monthly Excel export depends on the availability of the SAYED template and Excel COM automation.",
                "Sensitive configuration should be stored in appsettings.json locally and not committed to Git.",
                "Large database backups should be handled with Git LFS or an external backup/storage process."
            });
        });

        page.Footer().AlignCenter().Text(text =>
        {
            text.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Darken1));
            text.Span("Generated on ");
            text.Span(DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            text.Span(" | Page ");
            text.CurrentPageNumber();
            text.Span(" of ");
            text.TotalPages();
        });
    });
}).GeneratePdf(outputPath);

Console.WriteLine(outputPath);

static void Section(ColumnDescriptor col, string title, IEnumerable<string> bullets)
{
    col.Item().PaddingBottom(6).Text(title).FontSize(14).Bold().FontColor(Colors.Blue.Darken2);
    foreach (var bullet in bullets)
    {
        col.Item().PaddingBottom(3).Row(row =>
        {
            row.ConstantItem(12).Text("•").FontColor(Colors.Blue.Darken2);
            row.RelativeItem().Text(bullet).LineHeight(1.25f);
        });
    }
    col.Item().PaddingBottom(12);
}
