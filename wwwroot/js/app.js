(function () {
    var currentLang = 'ar';
    var currentUser = null;
    var leaveTypesCache = [];
    var leaveTypesRequest = null;
    var lastAttendanceRecords = null;
    var lastEmployeeRows = null;
    var lastBalanceRows = null;
    var lastLeaveTransactions = null;
    var lastMonthlyPreview = null;
    var lastMonthlyInfo = null;
    var lastDailyRows = null;
    var lastPermissionsAll = null;
    var lastAdminUsers = null;
    var lastAdminDays = null;
    var attendanceFilterOptions = null;
    var employeeFilterOptions = null;
    var exportFilterOptions = null;
    var leaveDaysRequestId = 0;
var attendancePage = 1;
    var attendancePageSize = 50;
    var attendanceTotalCount = 0;
    var attendanceTotalPages = 1;
    var attendanceRequestController = null;
    var overtimeRequestController = null;

    var i18n = {
        ar: {
            appTitle: 'نظام الحضور والإجازات',
            general: 'عام',
navAttendance: 'الحضور',
            navEmployees: 'الموظفون',
            navBalances: 'الأرصدة',
            navLeave: 'الإجازات',
            navMonthly: 'التقرير الشهري',
            navReports: 'التقارير',
            navAdmin: 'الإدارة',
            userName: 'حسام حسن',
            departmentName: 'تكنولوجيا المعلومات',
            langAr: 'العربية',
            langEn: 'الإنجليزية',
            roleEmployee: 'موظف موارد بشرية',
            roleAdmin: 'مدير النظام',
            loginTitle: 'تسجيل الدخول',
            loginDesc: 'أدخل اسم المستخدم وكلمة المرور.',
            password: 'كلمة المرور',
            rememberMe: 'تذكرني',
            login: 'دخول',
            logout: 'تسجيل الخروج',
            changePasswordTitle: 'تغيير كلمة المرور',
            changePasswordDesc: 'يجب تغيير كلمة المرور المؤقتة قبل المتابعة.',
            currentPassword: 'كلمة المرور الحالية',
            newPassword: 'كلمة المرور الجديدة',
            confirmPassword: 'تأكيد كلمة المرور',
            savePassword: 'حفظ كلمة المرور',
            temporaryPassword: 'كلمة المرور المؤقتة',
            refresh: 'تحديث',
            hrPortal: 'الموارد البشرية',
            workspaceKicker: 'لوحة التشغيل',
            workspaceTitle: 'الحضور والإجازات',
            workspaceDesc: 'بيانات الحضور والإجازات والتقارير الشهرية.',
            chipLiveData: 'بيانات الحضور',
            chipLiveDataDesc: 'قاعدة بيانات الحضور',
            chipMonthlyReports: 'تقارير شهرية',
            chipMonthlyReportsDesc: 'Excel / PDF',
            chipTemplates: 'استيراد البيانات',
            chipTemplatesDesc: 'استيراد وتحديث',
chooseFile: 'اختيار ملف',
            noFileChosen: 'لم يتم اختيار ملف',
            excelFileLabel: 'ملف Excel',
            fileFormatHint: 'صيغة Excel (.xlsx أو .xls)',
            invalidFileType: 'يرجى اختيار ملف Excel صحيح (.xlsx أو .xls)',
            importing: 'جارٍ الاستيراد...',
            clearFile: 'مسح الملف',
            importComplete: 'اكتمل الاستيراد بنجاح',
            dataFreshPending: 'جارٍ التحقق من حداثة البيانات...',
            dataFreshFailed: 'تعذر فحص حالة البيانات',
            lastUpdate: 'آخر تحديث:',
            attendanceKicker: 'الحضور',
            attendanceTitle: 'تقرير الحضور',
            attendanceDesc: 'استعرض سجلات الحضور والانصراف حسب الفترة وموظف محدد أو جميع الموظفين.',
            employeeNumbers: 'بحث الموظف',
            employeeNumbersPlaceholder: 'أدخل رقم الموظف أو اسمه، أو اتركه فارغًا لعرض الجميع',
            fromDate: 'من تاريخ',
            toDate: 'إلى تاريخ',
            showAttendance: 'عرض الحضور',
            attendanceResults: 'نتائج الحضور',
            exportExcel: 'تصدير إلى Excel',
            exportPdf: 'تصدير إلى PDF',
            employeesKicker: 'الموظفون',
            employeesTitle: 'بيانات الموظفين',
            employeesDesc: 'ابحث في قاعدة بيانات الموظفين أو اعرض كل الموظفين المسجلين.',
            searchNameOrNumber: 'بحث (اسم / رقم مالي)',
            allEmployeesPlaceholder: 'أدخل رقم الموظف أو اسمه، أو اتركه فارغًا لعرض الجميع',
            search: 'بحث',
            uploadEmployeesTitle: 'استيراد بيانات الموظفين',
            uploadEmployeesDesc: 'تحديث الأسماء والمسميات الوظيفية والإدارات والمستويات.',
            uploadData: 'استيراد البيانات',
            searchResults: 'نتائج البحث',
            balancesKicker: 'الأرصدة',
            balancesTitle: 'أرصدة الإجازات',
            balancesDesc: 'راجع أرصدة الإجازات السنوية لجميع الموظفين أو لموظف محدد.',
            financialNo: 'رقم مالي',
            allBalancesPlaceholder: 'أدخل رقم الموظف أو اسمه، أو اتركه فارغًا لعرض الجميع',
            showBalance: 'عرض الرصيد',
            uploadBalancesTitle: 'استيراد أرصدة الإجازات',
            uploadBalancesDesc: 'تحديث أرصدة الإجازات لجميع الموظفين من ملف Excel.',
            uploadBalances: 'استيراد الأرصدة',
            balance: 'الرصيد',
            leaveKicker: 'إدارة الإجازات',
            leaveTitle: 'إدارة الإجازات',
            leaveDesc: 'سجّل الإجازات وراجع الحركات واستورد بيانات الإجازات المجمّعة.',
            leaveFinancialPlaceholder: 'أدخل رقم الموظف أو اسمه، أو اتركه فارغًا لعرض الجميع',
            leaveType: 'نوع الإجازة',
            daysCount: 'عدد الأيام',
            reason: 'السبب',
            reasonPlaceholder: 'سبب الإجازة',
            grantLeave: 'تسجيل الإجازة',
            showLeaveLog: 'عرض سجل الإجازات',
            downloadLeaveTemplate: 'تحميل قالب الإجازات',
            downloadTemplate: 'تحميل القالب',
            uploadLeavesTitle: 'استيراد بيانات الإجازات',
            uploadLeavesDesc: 'إضافة عدة حركات إجازة دفعة واحدة.',
            uploadLeaves: 'استيراد الإجازات',
            transactions: 'حركات الإجازات',
            monthlyKicker: 'التقرير الشهري',
            monthlyTitle: 'كشف الحضور الشهري',
            monthlyDesc: 'أنشئ كشف الحضور الشهري من بيانات الحضور والإجازات حسب الشهر والإدارة.',
            year: 'السنة',
            month: 'الشهر',
            department: 'الإدارة',
            all: 'الكل',
            showReport: 'عرض التقرير',
            result: 'النتائج',
            loading: 'جارٍ التحميل...',
            footerText: 'نظام الحضور والإجازات'
            ,adminKicker: 'الإدارة'
            ,adminTitle: 'إدارة الصلاحيات والتقويم'
            ,adminDesc: 'أنشئ المستخدمين، اضبط الصلاحيات، وحدد أيام الراحة أو العمل المنزلي أو العطلات الرسمية.'
            ,username: 'اسم المستخدم'
            ,usernamePlaceholder: 'اسم المستخدم'
            ,displayName: 'الاسم الظاهر'
            ,displayNameAr: 'الاسم العربي'
            ,displayNameEn: 'الاسم الإنجليزي'
            ,displayNameEnPlaceholder: 'الاسم باللغة الإنجليزية'
            ,role: 'الدور'
            ,saveUser: 'حفظ المستخدم'
            ,calendarDate: 'التاريخ'
            ,dayType: 'نوع اليوم'
            ,notes: 'ملاحظات'
            ,saveDay: 'حفظ اليوم'
            ,adminResults: 'بيانات الإدارة'
        },
        en: {
            appTitle: 'Attendance and Leave System',
            general: 'General',
            navAttendance: 'Attendance',
            navEmployees: 'Employees',
            navBalances: 'Balances',
            navLeave: 'Leaves',
            navMonthly: 'Monthly Report',
            navAdmin: 'Admin',
            userName: 'Hossam Hassan',
            departmentName: 'Information Technology',
            langAr: 'Arabic',
            langEn: 'English',
            roleEmployee: 'HR Employee',
            roleAdmin: 'Administrator',
            loginTitle: 'Sign in',
            loginDesc: 'Enter your username and password.',
            password: 'Password',
            rememberMe: 'Remember me',
            login: 'Sign in',
            logout: 'Sign out',
            changePasswordTitle: 'Change password',
            changePasswordDesc: 'Change the temporary password before continuing.',
            currentPassword: 'Current password',
            newPassword: 'New password',
            confirmPassword: 'Confirm password',
            savePassword: 'Save password',
            temporaryPassword: 'Temporary password',
            refresh: 'Refresh',
            hrPortal: 'HR',
            workspaceKicker: 'Operations Workspace',
            workspaceTitle: 'Attendance and Leave',
            workspaceDesc: 'Attendance, leave, and monthly report data.',
            chipLiveData: 'Attendance Data',
            chipLiveDataDesc: 'SQL Server',
            chipMonthlyReports: 'Monthly Reports',
            chipMonthlyReportsDesc: 'Excel / PDF',
            chipTemplates: 'Data Import',
            chipTemplatesDesc: 'Upload and update',
            chooseFile: 'Choose File',
            noFileChosen: 'No file chosen',
            excelFileLabel: 'Excel file',
            fileFormatHint: 'Excel format (.xlsx or .xls)',
            invalidFileType: 'Please choose a valid Excel file (.xlsx or .xls)',
            importing: 'Importing...',
            clearFile: 'Clear file',
            importComplete: 'Import completed successfully',
            dataFreshPending: 'Checking data status...',
            dataFreshFailed: 'Unable to check data status',
            lastUpdate: 'Last update:',
            attendanceKicker: 'Attendance',
            attendanceTitle: 'Attendance Report',
            attendanceDesc: 'Review attendance and departure records by date range and employee, or all employees.',
            employeeNumbers: 'Employee search',
            employeeNumbersPlaceholder: 'Enter employee number or name, or leave empty to show all',
            fromDate: 'From date',
            toDate: 'To date',
            showAttendance: 'Show Attendance',
            attendanceResults: 'Attendance Results',
            exportExcel: 'Export Excel',
            exportPdf: 'Export PDF',
            employeesKicker: 'Employees',
            employeesTitle: 'Employee Data',
            employeesDesc: 'Search employee records or list all registered employees.',
            searchNameOrNumber: 'Search (name / financial number)',
            allEmployeesPlaceholder: 'Enter employee number or name, or leave empty to show all',
            search: 'Search',
            uploadEmployeesTitle: 'Upload Employee Data',
            uploadEmployeesDesc: 'Update names, titles, departments, and levels.',
            uploadData: 'Upload Data',
            searchResults: 'Search Results',
            balancesKicker: 'Balances',
            balancesTitle: 'Leave Balances',
            balancesDesc: 'Review annual leave quotas for all employees or a specific employee.',
            financialNo: 'Financial number',
            allBalancesPlaceholder: 'Enter employee number or name, or leave empty to show all',
            showBalance: 'Show Balance',
            uploadBalancesTitle: 'Upload Leave Balances',
            uploadBalancesDesc: 'Update leave quotas for all employees from an Excel file.',
            uploadBalances: 'Upload Balances',
            balance: 'Balance',
            leaveKicker: 'Leave Management',
            leaveTitle: 'Leave Management',
            leaveDesc: 'Grant leaves, review transactions, and upload bulk leave templates.',
            leaveFinancialPlaceholder: 'Enter employee number or name, or leave empty to show all',
            leaveType: 'Leave type',
            daysCount: 'Days count',
            reason: 'Reason',
            reasonPlaceholder: 'Leave reason',
            grantLeave: 'Grant Leave',
            showLeaveLog: 'Show Leave Log',
            downloadLeaveTemplate: 'Download Leave Template',
            downloadTemplate: 'Download Template',
            uploadLeavesTitle: 'Upload Leave Days',
            uploadLeavesDesc: 'Add multiple leave transactions at once.',
            uploadLeaves: 'Upload Leaves',
            transactions: 'Transactions',
            monthlyKicker: 'Monthly Report',
            monthlyTitle: 'Monthly Attendance Sheet',
            monthlyDesc: 'Generate the monthly attendance report from attendance and leave data by month and department.',
            year: 'Year',
            month: 'Month',
            department: 'Department',
            all: 'All',
            showReport: 'Show Report',
            result: 'Result',
            loading: 'Loading...',
            footerText: 'Attendance and Leave System'
            ,adminKicker: 'Admin'
            ,adminTitle: 'Permissions and Calendar'
            ,adminDesc: 'Create users, adjust permissions, and select weekly rest, work-from-home, or official holiday days.'
            ,username: 'Username'
            ,usernamePlaceholder: 'Username'
            ,displayName: 'Display name'
            ,displayNameAr: 'Arabic name'
            ,displayNameEn: 'English name'
            ,displayNameEnPlaceholder: 'English display name'
            ,role: 'Role'
            ,saveUser: 'Save User'
            ,calendarDate: 'Date'
            ,dayType: 'Day type'
            ,notes: 'Notes'
            ,saveDay: 'Save Day'
            ,adminResults: 'Admin Data'
        }
    };

    i18n.ar.resetPasswordTitle = 'إعادة تعيين كلمة المرور';
    i18n.ar.selectUser = 'اختر المستخدم';
    i18n.ar.resetPassword = 'إعادة تعيين كلمة المرور';
    i18n.ar.resetPasswordHint = 'سيُطلب من المستخدم تغيير كلمة المرور عند تسجيل الدخول التالي.';
    i18n.en.resetPasswordTitle = 'Reset password';
    i18n.en.selectUser = 'Select user';
    i18n.en.resetPassword = 'Reset password';
    i18n.en.resetPasswordHint = 'The user must change this password at the next sign-in.';
    i18n.ar.filterResults = 'تصفية النتائج';
    i18n.ar.sortResults = 'ترتيب النتائج';
    i18n.ar.searchMode = 'طريقة البحث';
    i18n.ar.searchContains = 'يحتوي';
    i18n.ar.searchExact = 'مطابقة تامة';
    i18n.ar.searchOneOf = 'أحد الأرقام';
    i18n.ar.sortDateNewest = 'التاريخ: الأحدث أولاً';
    i18n.ar.sortDateOldest = 'التاريخ: الأقدم أولاً';
    i18n.ar.sortEmployee = 'الموظف';
    i18n.ar.sortLeaveType = 'نوع الإجازة';
    i18n.ar.clearFilters = 'مسح عوامل التصفية';
    i18n.ar.employeeScheduleTitle = 'تعديل مواعيد الموظفين';
    i18n.ar.employeeScheduleHint = 'أدخل رقماً مالياً واحداً أو عدة أرقام مفصولة بفاصلة.';
    i18n.ar.employeeFinancialNumbers = 'الأرقام المالية';
    i18n.ar.scheduleStart = 'بداية العمل';
    i18n.ar.scheduleEnd = 'نهاية العمل';
    i18n.ar.saveSchedule = 'حفظ الموعد';
    i18n.ar.useLevelSchedule = 'استخدام موعد المستوى';
    i18n.ar.actions = 'الإجراءات';
    i18n.ar.edit = 'تعديل';
    i18n.ar.remove = 'حذف';
    i18n.ar.editLeave = 'تعديل الإجازة';
    i18n.ar.cancel = 'إلغاء';
    i18n.ar.saveChanges = 'حفظ التعديلات';
    i18n.ar.bulkImport = 'استيراد مجمع';
i18n.ar.navDaily = 'التقرير اليومي';
    i18n.ar.navReports = 'التقارير';
    i18n.ar.reportsSectionKicker = 'التقارير';
    i18n.ar.reportsSectionDesc = 'تقارير الإدارة العليا: العمل الإضافي والتقرير اليومي.';
    i18n.ar.reportsKicker = 'تقارير الإدارة العليا';
    i18n.ar.reportsTitle = 'التقرير الأول: العمل الإضافي للإدارة العليا';
    i18n.ar.reportsDesc = 'تقرير ساعات العمل الإضافي بعد 3:30 مساءً للإدارة العليا.';
    i18n.ar.fromDate = 'من تاريخ';
    i18n.ar.toDate = 'إلى تاريخ';
    i18n.ar.showReport = 'عرض التقرير';
    i18n.ar.overtimeResults = 'نتائج العمل الإضافي للإدارة العليا';
    i18n.ar.wageKicker = 'الأجر اليومي';
    i18n.ar.wageTitle = 'التقرير الثالث: أيام الحضور للأجر اليومي';
    i18n.ar.wageDesc = 'إجمالي أيام الحضور الشهرية لموظفي الأجر اليومي (مكافأة شاملة يومية).';
    i18n.ar.wageResults = 'نتائج الحضور للأجر اليومي';
    i18n.ar.filterCriteria = 'عوامل التصفية والخيارات';
    i18n.ar.attStatusFilter = 'الحالة';
    i18n.ar.level = 'المستوى';
    i18n.ar.standardJobs = 'وظائف نمطية';
    i18n.ar.area = 'المنطقة';
    i18n.ar.shift = 'الوردية';
    i18n.ar.sortByFinNo = 'الرقم المالي';
    i18n.ar.sortByName = 'الاسم';
    i18n.ar.dailyKicker = 'الإدارة العليا';
    i18n.ar.dailyTitle = 'التقرير الثاني: التقرير اليومي للإدارة العليا';
    i18n.ar.dailyDesc = 'راجع بصمات حضور وانصراف الإدارة العليا حسب التاريخ المحدد.';
    i18n.ar.dailyDate = 'تاريخ التقرير';
    i18n.ar.showDaily = 'عرض التقرير';
    i18n.ar.dailyResults = 'نتائج التقرير اليومي للإدارة العليا';
    i18n.ar.recalculateAttendance = 'إعادة احتساب الحالات';
    i18n.ar.recalculationConfirm = 'سيتم إعادة احتساب حالات الحضور للفترة والموظفين المحددين. هل تريد المتابعة؟';
    i18n.ar.recalculationComplete = 'اكتملت إعادة الاحتساب';
    i18n.ar.processedRecords = 'سجل تمت مراجعته';
    i18n.ar.updatedRecords = 'سجل تم تحديثه';
    i18n.ar.skippedRecords = 'سجل محفوظ يدوياً أو مرتبط بإجازة';
    i18n.ar.closeDialog = 'إغلاق النافذة';
    i18n.ar.displayNameArPlaceholder = 'اسم المستخدم بالعربية';
    i18n.ar.optional = 'اختياري';
    i18n.ar.scheduleFinancialNumbersPlaceholder = 'مثال: 4779, 4780';
    i18n.ar.requestLeaveTitle = 'طلب إجازة جديد';
    i18n.ar.requestLeave = 'إرسال الطلب';
    i18n.ar.requestLeaveSent = 'تم إرسال طلب الإجازة وسيتم مراجعته';
    i18n.ar.managerApprovals = 'طلبات بانتظار موافقة المدير';
    i18n.ar.hrApprovals = 'طلبات بانتظار موافقة الموارد البشرية';
    i18n.ar.approve = 'موافقة';
    i18n.ar.reject = 'رفض';
    i18n.ar.rejectionReasonPrompt = 'أدخل سبب الرفض (اختياري):';
    i18n.ar.rejectedMsg = 'تم رفض الطلب';
    i18n.ar.approvedMsg = 'تمت الموافقة على الطلب';
    i18n.ar.manager = 'المدير';
    i18n.ar.workflow = 'سير الموافقة';
    i18n.ar.workflowTitle = 'إدارة سير الموافقات';
    i18n.ar.workflowStatus = 'حالة سير الموافقة';
    i18n.ar.managerFinancialNo = 'الرقم المالي للمدير';
    i18n.ar.managerNoPlaceholder = 'اتركه فارغًا لإلغاء التعيين';
    i18n.ar.rejectionReason = 'سبب الرفض';
    i18n.ar.statusPendingManager = 'بانتظار المدير';
    i18n.ar.statusPendingHR = 'بانتظار الموارد البشرية';
    i18n.ar.statusApproved = 'معتمد';
    i18n.ar.statusRejected = 'مرفوض';
    i18n.ar.adSyncTitle = 'مزامنة Active Directory';
    i18n.ar.adSyncHint = 'مزامنة الموظفين من خادم Active Directory والحفاظ على تحديث بياناتهم.';
    i18n.ar.runAdSync = 'مزامنة الآن';
    i18n.ar.adSyncRunning = 'جارٍ المزامنة...';
    i18n.ar.adSyncLastRun = 'آخر مزامنة:';
    i18n.ar.adSyncNeverRun = 'لم يتم تشغيل المزامنة بعد';
    i18n.ar.managerAssignTitle = 'تعيين مدير لموظف';
    i18n.ar.saveManager = 'حفظ المدير';
    i18n.ar.managerSaved = 'تم حفظ المدير';
    i18n.ar.status = 'الحالة';
    i18n.ar.noPendingRequests = 'لا توجد طلبات معلقة';
    i18n.ar.requested = 'تم الطلب';
    i18n.en.scheduleFinancialNumbersPlaceholder = 'Example: 4779, 4780';
    i18n.en.filterResults = 'Filter results';
    i18n.en.sortResults = 'Sort results';
    i18n.en.searchMode = 'Search mode';
    i18n.en.searchContains = 'Contains';
    i18n.en.searchExact = 'Exact match';
    i18n.en.searchOneOf = 'One of';
    i18n.en.sortDateNewest = 'Date: newest first';
    i18n.en.sortDateOldest = 'Date: oldest first';
    i18n.en.sortEmployee = 'Employee';
    i18n.en.sortLeaveType = 'Leave type';
    i18n.en.clearFilters = 'Clear filters';
    i18n.en.employeeScheduleTitle = 'Employee schedules';
    i18n.en.employeeScheduleHint = 'Enter one financial number or multiple numbers separated by commas.';
    i18n.en.employeeFinancialNumbers = 'Financial numbers';
    i18n.en.scheduleStart = 'Start time';
    i18n.en.scheduleEnd = 'End time';
    i18n.en.saveSchedule = 'Save schedule';
    i18n.en.useLevelSchedule = 'Use level schedule';
    i18n.en.actions = 'Actions';
    i18n.en.edit = 'Edit';
    i18n.en.remove = 'Remove';
    i18n.en.editLeave = 'Edit leave';
    i18n.en.cancel = 'Cancel';
    i18n.en.saveChanges = 'Save changes';
    i18n.en.bulkImport = 'Bulk import';
i18n.en.navDaily = 'Daily Report';
    i18n.en.navReports = 'Reports';
    i18n.en.reportsSectionKicker = 'Reports';
    i18n.en.reportsSectionDesc = 'Top management reports: overtime and daily report.';
    i18n.en.reportsKicker = 'Top Management Reports';
    i18n.en.reportsTitle = 'First Report: Top Management Overtime';
    i18n.en.reportsDesc = 'Overtime hours after 3:30 PM for top management.';
    i18n.en.fromDate = 'From Date';
    i18n.en.toDate = 'To Date';
    i18n.en.showReport = 'Show Report';
    i18n.en.overtimeResults = 'Top Management Overtime Results';
    i18n.en.wageKicker = 'Daily Wage';
    i18n.en.wageTitle = 'Third Report: Daily-Wage Present Days';
    i18n.en.wageDesc = 'Total monthly present days for daily-wage staff (all-inclusive daily reward).';
    i18n.en.wageResults = 'Daily-Wage Present Results';
    i18n.en.filterCriteria = 'Filters and options';
    i18n.en.attStatusFilter = 'Status';
    i18n.en.level = 'Level';
    i18n.en.standardJobs = 'Standard Jobs';
    i18n.en.area = 'Area';
    i18n.en.shift = 'Schedule';
    i18n.en.sortByFinNo = 'Financial number';
    i18n.en.sortByName = 'Name';
    i18n.en.dailyKicker = 'Executive Attendance';
    i18n.en.dailyTitle = 'Second Report: Top Management Daily Report';
    i18n.en.dailyDesc = 'Review executive attendance punches for today or the selected date.';
    i18n.en.dailyDate = 'Report date';
    i18n.en.showDaily = 'Show Report';
    i18n.en.dailyResults = 'Top Management Daily Report Results';
    i18n.en.recalculateAttendance = 'Recalculate statuses';
    i18n.en.recalculationConfirm = 'Attendance statuses will be recalculated for the selected period and employees. Continue?';
    i18n.en.recalculationComplete = 'Recalculation complete';
    i18n.en.processedRecords = 'records reviewed';
    i18n.en.updatedRecords = 'records updated';
    i18n.en.skippedRecords = 'manual or leave records preserved';
    i18n.en.closeDialog = 'Close dialog';
    i18n.en.displayNameArPlaceholder = 'Arabic display name';
    i18n.en.optional = 'Optional';
i18n.en.scheduleFinancialNumbersPlaceholder = 'Example: 4779, 4780';
    i18n.en.requestLeaveTitle = 'New Leave Request';
    i18n.en.requestLeave = 'Submit Request';
    i18n.en.requestLeaveSent = 'Leave request submitted for review';
    i18n.en.managerApprovals = 'Awaiting Manager Approval';
    i18n.en.hrApprovals = 'Awaiting HR Approval';
    i18n.en.approve = 'Approve';
    i18n.en.reject = 'Reject';
    i18n.en.rejectionReasonPrompt = 'Enter rejection reason (optional):';
    i18n.en.rejectedMsg = 'Request rejected';
    i18n.en.approvedMsg = 'Request approved';
    i18n.en.manager = 'Manager';
    i18n.en.workflow = 'Workflow';
    i18n.en.workflowTitle = 'Approval Workflow';
    i18n.en.workflowStatus = 'Workflow status';
    i18n.en.managerFinancialNo = 'Manager financial number';
    i18n.en.managerNoPlaceholder = 'Leave empty to clear';
    i18n.en.rejectionReason = 'Rejection reason';
    i18n.en.statusPendingManager = 'Pending Manager';
    i18n.en.statusPendingHR = 'Pending HR';
    i18n.en.statusApproved = 'Approved';
    i18n.en.statusRejected = 'Rejected';
    i18n.en.adSyncTitle = 'Active Directory Sync';
    i18n.en.adSyncHint = 'Sync employees from the Active Directory server and keep their data up to date.';
    i18n.en.runAdSync = 'Sync Now';
    i18n.en.adSyncRunning = 'Syncing...';
    i18n.en.adSyncLastRun = 'Last sync:';
    i18n.en.adSyncNeverRun = 'Sync has not run yet';
    i18n.en.managerAssignTitle = 'Assign Manager to Employee';
    i18n.en.saveManager = 'Save Manager';
    i18n.en.managerSaved = 'Manager saved';
    i18n.en.status = 'Status';
    i18n.en.noPendingRequests = 'No pending requests';
    i18n.en.requested = 'Requested';
    i18n.ar.languageSelection = 'اختيار اللغة';
    i18n.ar.mainNavigation = 'التنقل الرئيسي';
    i18n.ar.workspaceSummary = 'ملخص مساحة العمل';
    i18n.ar.workspaceIndicators = 'مؤشرات العمل';
    i18n.ar.attendancePagination = 'صفحات نتائج الحضور';
    i18n.ar.rowsPerPage = 'عدد الصفوف';
    i18n.ar.previousPage = 'السابق';
    i18n.ar.nextPage = 'التالي';
    i18n.ar.scrollTableHint = 'مرر الجدول أفقياً لعرض بقية الأعمدة.';
    i18n.ar.totalRecords = 'سجل';
    i18n.en.languageSelection = 'Language selection';
    i18n.en.mainNavigation = 'Main navigation';
    i18n.en.workspaceSummary = 'Workspace summary';
    i18n.en.workspaceIndicators = 'Workspace indicators';
    i18n.en.attendancePagination = 'Attendance result pages';
    i18n.en.rowsPerPage = 'Rows per page';
    i18n.en.previousPage = 'Previous';
    i18n.en.nextPage = 'Next';
    i18n.en.scrollTableHint = 'Scroll the table horizontally to view the remaining columns.';
    i18n.en.totalRecords = 'records';

    var monthNames = {
        ar: ['يناير', 'فبراير', 'مارس', 'ابريل', 'مايو', 'يونيو', 'يوليو', 'اغسطس', 'سبتمبر', 'اكتوبر', 'نوفمبر', 'ديسمبر'],
        en: ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December']
    };

    var dayNames = {
        ar: ['الأحد', 'الإثنين', 'الثلاثاء', 'الأربعاء', 'الخميس', 'الجمعة', 'السبت'],
        en: ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']
    };

    var permissionLabels = {
        Attendance: { ar: 'الحضور', en: 'Attendance' },
        Employees: { ar: 'الموظفون', en: 'Employees' },
        Balances: { ar: 'الأرصدة', en: 'Balances' },
        Leaves: { ar: 'الإجازات', en: 'Leaves' },
        MonthlyReports: { ar: 'التقرير الشهري', en: 'Monthly Reports' },
        Imports: { ar: 'رفع البيانات', en: 'Imports' },
        Exports: { ar: 'التصدير', en: 'Exports' },
ManageCalendar: { ar: 'إدارة التقويم', en: 'Manage Calendar' },
        ManagePermissions: { ar: 'إدارة الصلاحيات', en: 'Manage Permissions' },
        CreateUsers: { ar: 'إنشاء المستخدمين', en: 'Create Users' },
        SelfAttendance: { ar: 'بياناتي الشخصية', en: 'My Attendance' },
        SelfLeave: { ar: 'طلبات الإجازة', en: 'My Leave' },
        LeaveApproval: { ar: 'موافقات المدير', en: 'Manager Approvals' },
        LeaveHR: { ar: 'موافقات الموارد البشرية', en: 'HR Approvals' },
        AdSync: { ar: 'مزامنة Active Directory', en: 'Active Directory Sync' }
    };

    function applyLanguage() {
        var dict = i18n[currentLang] || i18n.ar;

        document.querySelectorAll('[data-i18n]').forEach(function (el) {
            var key = el.getAttribute('data-i18n');
            if (dict[key] !== undefined) {
                el.textContent = dict[key];
            }
        });

        document.querySelectorAll('[data-i18n-placeholder]').forEach(function (el) {
            var key = el.getAttribute('data-i18n-placeholder');
            if (dict[key] !== undefined) {
                el.placeholder = dict[key];
            }
        });

        document.querySelectorAll('[data-i18n-aria-label]').forEach(function (el) {
            var key = el.getAttribute('data-i18n-aria-label');
            if (dict[key] !== undefined) {
                el.setAttribute('aria-label', dict[key]);
            }
        });

        document.querySelectorAll('.lang-pill').forEach(function (pill) {
            var isActiveLanguage = pill.getAttribute('data-lang') === currentLang;
            pill.classList.toggle('active-lang', isActiveLanguage);
            pill.setAttribute('aria-pressed', String(isActiveLanguage));
        });

        var todayEl = $('todayLabel');
        if (todayEl) {
            todayEl.textContent = fmtDate(new Date());
        }

        if (currentLang === 'ar') {
            document.documentElement.setAttribute('lang', 'ar');
            document.documentElement.setAttribute('dir', 'rtl');
        } else {
            document.documentElement.setAttribute('lang', 'en');
            document.documentElement.setAttribute('dir', 'ltr');
        }

        updateWorkspaceBrief();
        updateCurrentUserDisplay();
        updateRolePill();
        renderMonthOptions();
        renderLeaveTypeOptions();
        renderCalendarDayTypeOptions();
        renderAdminRoleOptions();
        renderAttendanceFilterOptions();
        renderEmployeeFilterOptions();
        renderExportFilterOptions();
        updateAttendanceActiveFilters();
        updateEmployeeActiveFilters();
        updateExportActiveFilters();
        if (currentUser) updateSyncStatus();
        updateFileLabels();
        if (lastAttendanceRecords && $('attResults') && $('attResults').style.display !== 'none') renderAttendance(lastAttendanceRecords);
        if (lastEmployeeRows && $('empResults') && $('empResults').style.display !== 'none') renderEmployees(lastEmployeeRows);
        if (lastBalanceRows && $('balResults') && $('balResults').style.display !== 'none') renderBalances(lastBalanceRows);
        if (lastLeaveTransactions && $('leaveResults') && $('leaveResults').style.display !== 'none') {
            renderLeaveTransactions(lastLeaveTransactions);
        }
        if (lastMonthlyPreview && $('exportPreview') && $('exportPreview').style.display !== 'none') renderMonthlyPreview(lastMonthlyPreview, lastMonthlyInfo);
        if (lastDailyRows && $('dailyResults') && $('dailyResults').style.display !== 'none') renderDaily(lastDailyRows);
        if (lastOvertimeRecords && $('reportsResults') && $('reportsResults').style.display !== 'none') renderOvertimeReport(lastOvertimeRecords);
        renderWageMonthOptions();
        if (lastWageRows && $('wageResults') && $('wageResults').style.display !== 'none') renderWageReport(lastWageRows);
        if (lastPermissionsAll && $('adminPermissions')) renderPermissions(lastPermissionsAll, getPermissionSelection());
        if (lastAdminUsers && lastAdminDays && $('adminResults') && $('adminResults').style.display !== 'none') renderAdminTables(lastAdminUsers, lastAdminDays);
    }

function hasPermission(permission) {
        return currentUser && Array.isArray(currentUser.permissions) && currentUser.permissions.indexOf(permission) >= 0;
    }

    function hasAnyPermission(permissions) {
        return String(permissions || '').split('|').some(function (permission) {
            return permission.trim() && hasPermission(permission.trim());
        });
    }

    function updateRolePill() {
        var el = $('rolePill');
        if (!el) return;
        el.textContent = roleLabel(currentUser && currentUser.role, currentUser && currentUser.isAdmin);
    }

    function updateCurrentUserDisplay() {
        if (!currentUser || !currentUser.displayName) return;
        var userLabel = document.querySelector('[data-i18n="userName"]');
        if (userLabel) userLabel.textContent = getUserDisplayName(currentUser);
    }

    function getUserDisplayName(user) {
        if (!user) return '-';
        if (currentLang === 'ar') {
            if (user.role === 'Admin' || user.isAdmin) return (i18n.ar || {}).roleAdmin || 'مدير النظام';
            if (isReadableDisplayName(user.displayNameAr)) return user.displayNameAr;
            return user.displayName || user.displayNameEn || user.username || '-';
        }
        return user.displayNameEn || user.displayName || user.displayNameAr || user.username || '-';
    }

    function isReadableDisplayName(value) {
        if (!value || !String(value).trim()) return false;
        var text = String(value).trim();
        return !/^[?\s]+$/.test(text) && text.indexOf('\uFFFD') < 0;
    }

    function roleLabel(role, isAdmin) {
        var dict = i18n[currentLang] || i18n.ar;
        return isAdmin || role === 'Admin' ? dict.roleAdmin : dict.roleEmployee;
    }

    function permissionLabel(permission) {
        var item = permissionLabels[permission];
        return item ? item[currentLang] : permission;
    }

    function permissionListLabel(permissions) {
        if (!permissions) return '-';
        var list = Array.isArray(permissions) ? permissions : String(permissions).split(',');
        return list.filter(Boolean).map(function (p) { return permissionLabel(p.trim()); }).join(currentLang === 'ar' ? '، ' : ', ');
    }

function applyPermissions() {
        document.querySelectorAll('[data-permission]').forEach(function (el) {
            var permissions = el.getAttribute('data-permission');
            el.style.display = hasAnyPermission(permissions) ? '' : 'none';
        });

        var selfOnly = currentUser && Array.isArray(currentUser.permissions)
            && currentUser.permissions.every(function (p) { return p === 'SelfAttendance' || p === 'SelfLeave'; });
        if (selfOnly) {
            document.querySelectorAll('.filter-card:not(#leaveRequestCard), .search-filter-card, .result-tool').forEach(function (el) {
                el.style.display = 'none';
            });
        }

        if (!hasAnyPermission('ManagePermissions|AdSync') && document.querySelector('.tab.active')?.dataset.tab === 'admin') {
            document.querySelector('[data-tab="attendance"]').click();
        }
    }

    function showAuthError(message) {
        var el = $('authError');
        if (!el) return;
        el.textContent = message || (currentLang === 'ar' ? 'تعذر إكمال الطلب' : 'Unable to complete the request');
        el.style.display = '';
    }

    function clearAuthError() {
        var el = $('authError');
        if (el) el.style.display = 'none';
    }

    function showLoginScreen(changePassword) {
        $('loginScreen').style.display = 'grid';
        $('appShell').style.display = 'none';
        $('loginPanel').style.display = changePassword ? 'none' : '';
        $('changePasswordPanel').style.display = changePassword ? '' : 'none';
        clearAuthError();
    }

function showApplication() {
        $('loginScreen').style.display = 'none';
        $('appShell').style.display = '';
        updateCurrentUserDisplay();
        updateRolePill();
        applyPermissions();
        configureSelfServiceUI();
        restoreActiveTab();
        loadLeaveTypes();
        loadAttendanceFilterOptions();
        loadEmployeeFilterOptions();
        loadExportFilterOptions();
        updateSyncStatus();
        loadApprovalQueues();
    }

    function configureSelfServiceUI() {
        if (!currentUser) return;
        var selfScoped = hasPermission('SelfAttendance') && !hasPermission('Attendance');
        if (selfScoped) {
            var employeeNo = currentUser.employeeNo || '';
            var attEmp = $('attEmployees');
            if (attEmp) { attEmp.value = employeeNo; attEmp.disabled = true; }
            if ($('attMatchMode')) { $('attMatchMode').value = 'exact'; $('attMatchMode').disabled = true; }
            if ($('balFinNo')) { $('balFinNo').value = employeeNo; $('balFinNo').disabled = true; }
            if ($('leaveFinNo')) { $('leaveFinNo').value = employeeNo; $('leaveFinNo').disabled = true; }
        }
    }

    function loadCurrentUser() {
        return fetch('/api/auth/me', { cache: 'no-store', credentials: 'same-origin' })
            .then(function (r) {
                if (!r.ok) throw new Error('Unauthorized');
                return r.json();
            })
            .then(function (user) {
                currentUser = user;
                if (user.mustChangePassword) showLoginScreen(true);
                else showApplication();
                return user;
            })
            .catch(function () {
                currentUser = null;
                showLoginScreen(false);
            });
    }

    $('loginForm').addEventListener('submit', function (event) {
        event.preventDefault();
        clearAuthError();
        var password = $('loginPassword').value;
        fetch('/api/auth/login', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'same-origin',
            body: JSON.stringify({
                username: $('loginUsername').value.trim(),
                password: password,
                rememberMe: $('loginRemember').checked
            })
        }).then(function (r) {
            if (!r.ok) return r.json().then(function (x) { throw new Error(x.error || 'Login failed'); });
            return r.json();
        }).then(function (user) {
            currentUser = user;
            $('currentPassword').value = password;
            $('loginPassword').value = '';
            if (user.mustChangePassword) showLoginScreen(true);
            else showApplication();
        }).catch(function (error) {
            showAuthError(currentLang === 'ar' ? 'اسم المستخدم أو كلمة المرور غير صحيحة' : error.message);
        });
    });

    $('changePasswordForm').addEventListener('submit', function (event) {
        event.preventDefault();
        clearAuthError();
        var newPassword = $('newPassword').value;
        if (newPassword !== $('confirmPassword').value) {
            showAuthError(currentLang === 'ar' ? 'كلمتا المرور غير متطابقتين' : 'Passwords do not match');
            return;
        }
        fetch('/api/auth/change-password', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'same-origin',
            body: JSON.stringify({
                currentPassword: $('currentPassword').value,
                newPassword: newPassword
            })
        }).then(function (r) {
            if (!r.ok) return r.json().then(function (x) { throw new Error(x.error || 'Password change failed'); });
            return r.json();
        }).then(function (user) {
            currentUser = user;
            $('changePasswordForm').reset();
            showApplication();
        }).catch(function (error) { showAuthError(error.message); });
    });

    $('logoutBtn').addEventListener('click', function () {
        fetch('/api/auth/logout', { method: 'POST', credentials: 'same-origin' })
            .finally(function () {
                currentUser = null;
                showLoginScreen(false);
            });
    });

    function updateWorkspaceBrief() {
        var activeTab = document.querySelector('.tab.active');
        var module = activeTab ? activeTab.getAttribute('data-tab') : 'attendance';
        var titleMap = {
            attendance: 'attendanceTitle',
            employees: 'employeesTitle',
            balances: 'balancesTitle',
            leave: 'leaveTitle',
            export: 'monthlyTitle',
            daily: 'dailyTitle',
            reports: 'navReports',
            admin: 'adminTitle'
        };
        var descMap = {
            attendance: 'attendanceDesc',
            employees: 'employeesDesc',
            balances: 'balancesDesc',
            leave: 'leaveDesc',
            export: 'monthlyDesc',
            daily: 'dailyDesc',
            reports: 'reportsSectionDesc',
            admin: 'adminDesc'
        };
        var dict = i18n[currentLang] || i18n.ar;
        var title = $('workspaceTitle');
        var desc = $('workspaceDesc');
        if (title && titleMap[module]) title.textContent = dict[titleMap[module]] || dict.workspaceTitle;
        if (desc && descMap[module]) desc.textContent = dict[descMap[module]] || dict.workspaceDesc;
    }

    document.querySelectorAll('.lang-pill').forEach(function (pill) {
        pill.addEventListener('click', function () {
            currentLang = this.getAttribute('data-lang') || 'ar';
            applyLanguage();
        });
    });

    document.querySelectorAll('button').forEach(function (button) {
        button.addEventListener('pointermove', function (event) {
            var rect = button.getBoundingClientRect();
            button.style.setProperty('--ripple-x', (event.clientX - rect.left) + 'px');
            button.style.setProperty('--ripple-y', (event.clientY - rect.top) + 'px');
        });
    });

var validExcelExt = /\.(xlsx|xls)$/i;
    var uploadMeta = {
        employeesUploadFile: { button: 'uploadEmployeesBtn', url: '/api/import/employees', labelKey: 'uploadData' },
        balancesUploadFile: { button: 'uploadBalancesBtn', url: '/api/import/balances', labelKey: 'uploadBalances' },
        leavesUploadFile: { button: 'uploadLeavesBtn', url: '/api/import/leaves', labelKey: 'uploadLeaves' }
    };

    function formatFileSize(bytes) {
        if (!bytes) return '0 B';
        if (bytes < 1024) return bytes + ' B';
        if (bytes < 1048576) return (bytes / 1024).toFixed(1) + ' KB';
        return (bytes / 1048576).toFixed(1) + ' MB';
    }

    function updateFileLabels() {
        var dict = i18n[currentLang] || i18n.ar;
        Object.keys(uploadMeta).forEach(function (inputId) {
            var input = $(inputId);
            if (!input) return;
            var picker = input.closest('.file-picker');
            var nameEl = document.querySelector('[data-file-name-for="' + inputId + '"]');
            var btn = $(uploadMeta[inputId].button);
            var file = input.files && input.files.length ? input.files[0] : null;

            if (picker) picker.classList.remove('has-file', 'is-invalid');
            if (file) {
                var ok = validExcelExt.test(file.name);
                if (picker) picker.classList.add(ok ? 'has-file' : 'is-invalid');
                if (nameEl) nameEl.textContent = file.name + (ok ? '  (' + formatFileSize(file.size) + ')' : '');
                if (btn) btn.disabled = !ok;
            } else {
                if (nameEl) nameEl.textContent = dict.noFileChosen;
                if (btn) btn.disabled = true;
            }
        });
    }

document.querySelectorAll('input[type="file"]').forEach(function (input) {
        input.addEventListener('change', updateFileLabels);
    });

    document.querySelectorAll('[data-drop-zone]').forEach(function (zone) {
        ['dragenter', 'dragover'].forEach(function (evt) {
            zone.addEventListener(evt, function (e) {
                e.preventDefault();
                zone.classList.add('is-dragging');
            });
        });
        ['dragleave', 'drop'].forEach(function (evt) {
            zone.addEventListener(evt, function (e) {
                e.preventDefault();
                zone.classList.remove('is-dragging');
            });
        });
        zone.addEventListener('drop', function (e) {
            var input = zone.querySelector('input[type="file"]');
            if (!input) return;
            var files = e.dataTransfer && e.dataTransfer.files;
            if (files && files.length) {
                var dt = new DataTransfer();
                Array.prototype.forEach.call(files, function (file) { dt.items.add(file); });
                input.files = dt.files;
                updateFileLabels();
            }
        });
    });

    document.querySelectorAll('[data-clear-file]').forEach(function (btn) {
        btn.addEventListener('click', function () {
            var input = $(btn.getAttribute('data-clear-file'));
            if (!input) return;
            input.value = '';
            updateFileLabels();
        });
    });

    function showSuccess(msg) {
        $('successMessage').textContent = msg;
        show('success');
        clearTimeout(showSuccess._timer);
        showSuccess._timer = setTimeout(function () { hide('success'); }, 8000);
    }

    function downloadBlobFile(url, fileName) {
        var sep = url.indexOf('?') >= 0 ? '&' : '?';
        fetch(url + sep + '_=' + Date.now(), { cache: 'no-store' })
            .then(function (response) {
                if (!response.ok) throw new Error(currentLang === 'ar' ? 'تعذر تحميل القالب' : 'Could not download template');
                return response.blob();
            })
            .then(function (blob) {
                var objectUrl = URL.createObjectURL(blob);
                var link = document.createElement('a');
                link.href = objectUrl;
                link.download = fileName;
                document.body.appendChild(link);
                link.click();
                link.remove();
                URL.revokeObjectURL(objectUrl);
            })
            .catch(function (error) { showError(error.message); });
    }

    function performUpload(inputId, onSuccess) {
        var meta = uploadMeta[inputId];
        var input = $(inputId);
        var btn = $(meta.button);
        var file = input.files && input.files.length ? input.files[0] : null;
        if (!file) {
            showError(currentLang === 'ar' ? 'يرجى اختيار ملف' : 'Please select a file');
            return;
        }
        if (!validExcelExt.test(file.name)) {
            showError(i18n[currentLang].invalidFileType);
            return;
        }

        btn.classList.add('busy');
        btn.textContent = i18n[currentLang].importing;
        btn.disabled = true;
        hideError();

        var fd = new FormData();
        fd.append('file', file);

        fetch(meta.url, { method: 'POST', body: fd })
            .then(readUploadResponse)
            .then(function (data) {
                input.value = '';
                btn.classList.remove('busy');
                updateFileLabels();
                btn.textContent = i18n[currentLang][meta.labelKey];
                showSuccess(uploadSuccessText(data));
                onSuccess(data);
            })
            .catch(function (err) {
                btn.classList.remove('busy');
                btn.textContent = i18n[currentLang][meta.labelKey];
                updateFileLabels();
                showError(err.message);
            });
    }

    function $(id) { return document.getElementById(id); }
    function show(id) { var el = $(id); if (el) el.style.display = 'block'; }
    function hide(id) { var el = $(id); if (el) el.style.display = 'none'; }

    function userFriendlyError(msg) {
        var value = String(msg || '');
        if (/duplicate|unique constraint|already exists/i.test(value)) {
            return currentLang === 'ar' ? 'السجل موجود بالفعل. راجع البيانات ثم أعد المحاولة.' : 'This record already exists. Review the data and try again.';
        }
        if (/failed to fetch|networkerror|network request|status code 5\d\d|sql/i.test(value)) {
            return currentLang === 'ar' ? 'تعذر إتمام العملية حالياً. تحقق من الاتصال ثم أعد المحاولة.' : 'The operation could not be completed. Check the connection and try again.';
        }
        return value || (currentLang === 'ar' ? 'حدث خطأ غير متوقع.' : 'An unexpected error occurred.');
    }

    function showError(msg) {
        $('errorMessage').textContent = userFriendlyError(msg);
        show('error');
    }

    window.addEventListener('unhandledrejection', function (event) {
        event.preventDefault();
        showError(event.reason && event.reason.message ? event.reason.message : event.reason);
    });

    function hideError() { hide('error'); }

    function showLoading() {
        show('loading');
        hide('error');
        hide('success');
    }

    function hideLoading() { hide('loading'); }

    function fmtDate(d) {
        if (!d) return '-';
        if (typeof d === 'string') {
            var isoDate = d.match(/^(\d{4})-(\d{2})-(\d{2})/);
            if (isoDate) return isoDate[1] + '-' + isoDate[2] + '-' + isoDate[3];
        }
        var dt = d instanceof Date ? d : new Date(d);
        if (isNaN(dt.getTime())) return '-';
        return dt.getFullYear() + '-' +
            String(dt.getMonth() + 1).padStart(2, '0') + '-' +
            String(dt.getDate()).padStart(2, '0');
    }

    function fmtDateLocal(d) {
        return fmtDate(d);
    }

    function fmtDateTime(d) {
        if (!d) return '-';
        var dt = new Date(d);
        if (isNaN(dt.getTime())) return '-';
        return fmtDate(dt) + ' ' +
            String(dt.getHours()).padStart(2, '0') + ':' +
            String(dt.getMinutes()).padStart(2, '0');
    }

    var arMonths = ['يناير', 'فبراير', 'مارس', 'ابريل', 'مايو', 'يونيو', 'يوليو', 'اغسطس', 'سبتمبر', 'اكتوبر', 'نوفمبر', 'ديسمبر'];
    var arDays = ['الأحد', 'الإثنين', 'الثلاثاء', 'الأربعاء', 'الخميس', 'الجمعة', 'السبت'];

    function fmtDayName(d) {
        if (!d) return '-';
        var dt = new Date(d);
        return (dayNames[currentLang] || dayNames.en)[parseInt(dt.getDay(), 10)];
    }

    function fmtDayNameAr(d) {
        if (!d) return '-';
        var dt = new Date(d);
        return arDays[parseInt(dt.getDay(), 10)];
    }

    function fmtDateAr(d) {
        if (!d) return '-';
        var dt = new Date(d);
        var m = parseInt(dt.getMonth(), 10);
        var day = parseInt(dt.getDate(), 10);
        var y = dt.getFullYear();
        return day + ' ' + arMonths[m] + ' ' + y;
    }

    function fmtTimeAr(d) {
        if (!d) return '-';
        var dt = new Date(d);
        var h = parseInt(dt.getHours(), 10);
        var mi = String(dt.getMinutes()).padStart(2, '0');
        var ampm = h >= 12 ? 'م' : 'ص';
        h = h % 12 || 12;
        return h + ':' + mi + ' ' + ampm;
    }

    function fmtTime(d) {
        if (!d) return '-';
        if (currentLang === 'ar') return fmtTimeAr(d);
        var dt = new Date(d);
        var h = String(dt.getHours()).padStart(2, '0');
        var mi = String(dt.getMinutes()).padStart(2, '0');
        return h + ':' + mi;
    }

    function fmtScheduleAr(time) {
        if (!time) return '-';
        var parts = time.split(':');
        var h = parseInt(parts[0], 10);
        var m = parts[1] || '00';
        var ampm = h >= 12 ? 'م' : 'ص';
        h = h % 12 || 12;
        return h + ':' + m + ' ' + ampm;
    }

    function fmtSchedule(time) {
        if (!time) return '-';
        if (currentLang === 'ar') return fmtScheduleAr(time);
        var parts = time.split(':');
        return (parts[0] || '00') + ':' + (parts[1] || '00');
    }

    function fmtDuration(minutes) {
        var dh = Math.floor(minutes / 60);
        var dm = minutes % 60;
        return currentLang === 'ar' ? dh + ' س ' + dm + ' د' : dh + 'h ' + dm + 'm';
    }

var statusArMap = {
        'Present': 'حاضر', 'Pending': 'قيد الانتظار', 'Checked In': 'حاضر بدون انصراف', 'Missing Check Out': 'لم يسجل انصراف', 'Late': 'متأخر', 'Early Leave': 'انصراف مبكر', 'Absent': 'غائب',
        'Leave': 'إجازة', 'Holiday': 'عطلة', 'Weekly Rest': 'راحة أسبوعية', 'Work From Home': 'عمل من المنزل',
        'Mission': 'مأمورية', 'Training': 'دورة تدريب',
        'PendingManager': 'بانتظار المدير', 'PendingHR': 'بانتظار الموارد البشرية', 'Approved': 'معتمد', 'Rejected': 'مرفوض'
    };

    var statusEnMap = {
        'Present': 'Present', 'Pending': 'Pending', 'Checked In': 'Checked In', 'Missing Check Out': 'Missing Check Out', 'Late': 'Late', 'Early Leave': 'Early Leave', 'Absent': 'Absent',
        'Leave': 'Leave', 'Holiday': 'Holiday', 'Weekly Rest': 'Weekly Rest', 'Work From Home': 'Work From Home',
        'Mission': 'Mission', 'Training': 'Training',
        'PendingManager': 'Pending Manager', 'PendingHR': 'Pending HR', 'Approved': 'Approved', 'Rejected': 'Rejected'
    };

    function statusLabel(status) {
        return currentLang === 'ar' ? (statusArMap[status] || status) : (statusEnMap[status] || status);
    }

function badge(status) {
        var map = {
            'Present': 'badge-present', 'Pending': 'badge-late', 'Checked In': 'badge-late', 'Missing Check Out': 'badge-absent', 'Late': 'badge-late', 'Early Leave': 'badge-absent', 'Absent': 'badge-absent',
            'Leave': 'badge-leave', 'Holiday': 'badge-holiday', 'Weekly Rest': 'badge-holiday', 'Work From Home': 'badge-leave',
            'Approved': 'badge-present', 'PendingManager': 'badge-late', 'PendingHR': 'badge-late', 'Rejected': 'badge-absent'
        };
        var cls = map[status] || 'badge-present';
        return '<span class="' + cls + '">' + statusLabel(status) + '</span>';
    }

    function clear(el) { el.innerHTML = ''; }

    function tableScrollOpen(tableLabel) {
        return '<p class="table-scroll-hint">' + i18n[currentLang].scrollTableHint + '</p>' +
            '<div class="table-scroll attendance-table-scroll" tabindex="0" role="region" aria-label="' + tableLabel + '">';
    }

    function emptyState(message, hint) {
        var sub = hint ? '<span>' + hint + '</span>' : '';
        return '<div class="empty-state"><strong>' + message + '</strong>' + sub + '</div>';
    }

function uploadSuccessText(data) {
        if (data && data.message) return data.message;
        return currentLang === 'ar' ? 'تم استيراد الملف ومعالجة البيانات بنجاح.' : 'Upload completed successfully.';
    }

    function readUploadResponse(response) {
        return response.json().catch(function () {
            return {};
        }).then(function (data) {
            if (!response.ok) {
                throw new Error(data.error || (currentLang === 'ar'
                    ? 'تعذر استيراد الملف'
                    : 'Could not import the file'));
            }
            return data;
        });
    }

function renderLeaveTypeOptions() {
        var sel = $('leaveTypeId');
        if (!sel) return;
        var selected = sel.value;
        sel.innerHTML = '';
        var allOpt = document.createElement('option');
        allOpt.value = '';
        allOpt.textContent = currentLang === 'ar' ? 'كل أنواع الإجازات' : 'All leave types';
        sel.appendChild(allOpt);
        leaveTypesCache.forEach(function (t) {
            var opt = document.createElement('option');
            opt.value = t.id;
            opt.textContent = (currentLang === 'ar' ? (t.nameAr || t.nameEn) : (t.nameEn || t.nameAr)) + ' (' + t.code + ')';
            sel.appendChild(opt);
        });
        sel.value = selected;

        var editSelect = $('editLeaveTypeId');
        if (editSelect) {
            var editSelected = editSelect.value;
            editSelect.replaceChildren();
            leaveTypesCache.forEach(function (type) {
                var editOption = document.createElement('option');
                editOption.value = type.id;
                editOption.textContent = (currentLang === 'ar' ? (type.nameAr || type.nameEn) : (type.nameEn || type.nameAr)) + ' (' + type.code + ')';
                editSelect.appendChild(editOption);
            });
            editSelect.value = editSelected;
        }

        var requestSelect = $('reqLeaveTypeId');
        if (requestSelect) {
            var requestSelected = requestSelect.value;
            requestSelect.replaceChildren();
            leaveTypesCache.forEach(function (type) {
                var requestOption = document.createElement('option');
                requestOption.value = type.id;
                requestOption.textContent = (currentLang === 'ar' ? (type.nameAr || type.nameEn) : (type.nameEn || type.nameAr)) + ' (' + type.code + ')';
                requestSelect.appendChild(requestOption);
            });
            requestSelect.value = requestSelected;
        }
    }

    function renderMonthOptions() {
        var sel = $('exportMonth');
        if (!sel) return;
        var now = new Date();
        var selected = parseInt(sel.value || '1', 10);
        var selectedYear = parseInt($('exportYear').value || String(now.getFullYear()), 10);
        var monthCount = selectedYear < now.getFullYear()
            ? 12
            : (selectedYear === now.getFullYear() ? now.getMonth() + 1 : 0);
        sel.innerHTML = '';
        (monthNames[currentLang] || monthNames.en).slice(0, monthCount).forEach(function (name, index) {
            var opt = document.createElement('option');
            opt.value = String(index + 1);
            opt.textContent = name;
            sel.appendChild(opt);
        });
        if (monthCount > 0) sel.value = String(Math.min(Math.max(selected, 1), monthCount));
        sel.disabled = monthCount === 0;
    }

    function renderCalendarDayTypeOptions() {
        var sel = $('calendarDayType');
        if (!sel) return;
        var selected = sel.value || 'Weekly Rest';
        sel.innerHTML = '';
        ['Weekly Rest', 'Work From Home', 'Holiday'].forEach(function (type) {
            var opt = document.createElement('option');
            opt.value = type;
            opt.textContent = statusLabel(type);
            sel.appendChild(opt);
        });
        sel.value = selected;
    }

    function renderAdminRoleOptions() {
        var sel = $('adminRole');
        if (!sel) return;
        var selected = sel.value || 'Employee';
        sel.innerHTML = '';
        [
            { value: 'Admin', label: roleLabel('Admin', true) },
            { value: 'Employee', label: roleLabel('Employee', false) }
        ].forEach(function (role) {
            var opt = document.createElement('option');
            opt.value = role.value;
            opt.textContent = role.label;
            sel.appendChild(opt);
        });
        sel.value = selected;
    }

    function optionList(source, name) {
        if (!source) return [];
        return source[name] || source[name.charAt(0).toUpperCase() + name.slice(1)] || [];
    }

    function setSelectOptions(id, values, formatter) {
        var sel = $(id);
        if (!sel) return;
        var selected = sel.value;
        var dict = i18n[currentLang] || i18n.ar;
        sel.innerHTML = '';
        var allOpt = document.createElement('option');
        allOpt.value = '';
        allOpt.textContent = dict.all || 'All';
        sel.appendChild(allOpt);
        (values || []).forEach(function (value) {
            if (value === null || value === undefined || String(value).trim() === '') return;
            var opt = document.createElement('option');
            opt.value = String(value);
            opt.textContent = formatter ? formatter(value) : String(value);
            sel.appendChild(opt);
        });
        sel.value = Array.from(sel.options).some(function (opt) { return opt.value === selected; }) ? selected : '';
    }

    function appendStandardJobsOption(id) {
        var sel = $(id);
        if (!sel) return;
        var dict = i18n[currentLang] || i18n.ar;
        var selected = sel.value;
        var opt = document.createElement('option');
        opt.value = 'وظائف نمطية';
        opt.textContent = dict.standardJobs || 'وظائف نمطية';
        sel.appendChild(opt);
        if (selected === 'وظائف نمطية') sel.value = selected;
    }

    function renderAttendanceFilterOptions() {
        var statusValues = ['Present', 'Pending', 'Checked In', 'Missing Check Out', 'Late', 'Early Leave', 'Absent', 'Leave', 'Holiday', 'Weekly Rest', 'Work From Home', 'Mission', 'Training'];
        setSelectOptions('attStatus', statusValues, statusLabel);
        if (!attendanceFilterOptions) return;
        setSelectOptions('attDepartment', optionList(attendanceFilterOptions, 'departments'));
        setSelectOptions('attLevel', optionList(attendanceFilterOptions, 'levels'));
        appendStandardJobsOption('attLevel');
        setSelectOptions('attArea', optionList(attendanceFilterOptions, 'areas'));
        setSelectOptions('attSchedule', optionList(attendanceFilterOptions, 'schedules'));
    }

    function loadAttendanceFilterOptions() {
        fetch('/api/tracking/filter-options?_=' + Date.now(), { cache: 'no-store', credentials: 'same-origin' })
            .then(function (r) {
                if (!r.ok) throw new Error('Unable to load attendance filters');
                return r.json();
            })
            .then(function (options) {
                attendanceFilterOptions = options || {};
                renderAttendanceFilterOptions();
            })
            .catch(function () {
                attendanceFilterOptions = attendanceFilterOptions || {};
                renderAttendanceFilterOptions();
            });
    }

    function renderEmployeeFilterOptions() {
        if (!employeeFilterOptions) return;
        setSelectOptions('empDepartment', optionList(employeeFilterOptions, 'departments'));
        setSelectOptions('empLevel', optionList(employeeFilterOptions, 'levels'));
        appendStandardJobsOption('empLevel');
        setSelectOptions('empArea', optionList(employeeFilterOptions, 'areas'));
        setSelectOptions('empStatus', optionList(employeeFilterOptions, 'statuses'));
    }

    function loadEmployeeFilterOptions() {
        fetch('/api/employees/filter-options?_=' + Date.now(), { cache: 'no-store', credentials: 'same-origin' })
            .then(function (r) {
                if (!r.ok) throw new Error('Unable to load employee filters');
                return r.json();
            })
            .then(function (options) {
                employeeFilterOptions = options || {};
                renderEmployeeFilterOptions();
            })
            .catch(function () {
                employeeFilterOptions = employeeFilterOptions || {};
                renderEmployeeFilterOptions();
            });
    }

    function renderExportFilterOptions() {
        if (!exportFilterOptions) return;
        setSelectOptions('exportDept', optionList(exportFilterOptions, 'departments'));
        setSelectOptions('exportLevel', optionList(exportFilterOptions, 'levels'));
        appendStandardJobsOption('exportLevel');
        setSelectOptions('exportArea', optionList(exportFilterOptions, 'areas'));
    }

    function loadExportFilterOptions() {
        fetch('/api/export/filter-options?_=' + Date.now(), { cache: 'no-store', credentials: 'same-origin' })
            .then(function (r) {
                if (!r.ok) throw new Error('Unable to load monthly report filters');
                return r.json();
            })
            .then(function (options) {
                exportFilterOptions = options || {};
                renderExportFilterOptions();
            })
            .catch(function () {
                exportFilterOptions = exportFilterOptions || {};
                renderExportFilterOptions();
            });
    }

    function selectedScheduleRange() {
        var value = $('attSchedule') ? $('attSchedule').value : '';
        if (!value) return null;
        var parts = value.split(/\s+-\s+/);
        if (parts.length < 2) return null;
        return { start: parts[0], end: parts[1] };
    }

    function resetAttendanceFilters() {
        $('attEmployees').value = '';
        if ($('attMatchMode')) $('attMatchMode').value = 'contains';
        $('attFrom').valueAsDate = new Date();
        $('attTo').valueAsDate = new Date();
        ['attDepartment', 'attStatus', 'attLevel', 'attArea', 'attSchedule'].forEach(function (id) {
            if ($(id)) $(id).value = '';
        });
        updateAttendanceActiveFilters();
    }

    function updateAttendanceActiveFilters() {
        var countEl = $('attActiveFilterCount');
        var chipsEl = $('attActiveFilterChips');
        var filters = [
            { id: 'attEmployees', value: $('attEmployees') && $('attEmployees').value.trim() },
            { id: 'attDepartment', value: $('attDepartment') && $('attDepartment').value },
            { id: 'attStatus', value: $('attStatus') && $('attStatus').selectedOptions[0] && $('attStatus').selectedOptions[0].textContent, raw: $('attStatus') && $('attStatus').value },
            { id: 'attLevel', value: $('attLevel') && $('attLevel').value },
            { id: 'attArea', value: $('attArea') && $('attArea').value },
            { id: 'attSchedule', value: $('attSchedule') && $('attSchedule').value }
        ].filter(function (item) { return item.raw !== '' && item.value; });

        if (countEl) {
            countEl.dataset.count = String(filters.length);
            countEl.textContent = filters.length
                ? (currentLang === 'ar' ? filters.length + ' \u0639\u0648\u0627\u0645\u0644' : filters.length + ' filters')
                : '';
        }

        if (chipsEl) {
            chipsEl.innerHTML = '';
            filters.forEach(function (item) {
                var label = document.querySelector('label[for="' + item.id + '"]');
                var chip = document.createElement('span');
                chip.textContent = (label ? label.textContent : item.id) + ': ' + item.value;
                chipsEl.appendChild(chip);
            });
        }
    }

    function updateFilterChipSummary(countId, chipsId, filters) {
        var countEl = $(countId);
        var chipsEl = $(chipsId);
        var active = (filters || []).filter(function (item) {
            return item.value !== null && item.value !== undefined && String(item.value).trim() !== '';
        });

        if (countEl) {
            countEl.dataset.count = String(active.length);
            countEl.textContent = active.length
                ? (currentLang === 'ar' ? active.length + ' \u0639\u0648\u0627\u0645\u0644' : active.length + ' filters')
                : '';
        }

        if (chipsEl) {
            chipsEl.innerHTML = '';
            active.forEach(function (item) {
                var label = document.querySelector('label[for="' + item.id + '"]');
                var chip = document.createElement('span');
                chip.textContent = (label ? label.textContent : item.id) + ': ' + item.value;
                chipsEl.appendChild(chip);
            });
        }
    }

    function updateEmployeeActiveFilters() {
        updateFilterChipSummary('empActiveFilterCount', 'empActiveFilterChips', [
            { id: 'empSearch', value: $('empSearch') && $('empSearch').value.trim() },
            { id: 'empDepartment', value: $('empDepartment') && $('empDepartment').value },
            { id: 'empLevel', value: $('empLevel') && $('empLevel').value },
            { id: 'empArea', value: $('empArea') && $('empArea').value },
            { id: 'empStatus', value: $('empStatus') && $('empStatus').value }
        ]);
    }

    function updateExportActiveFilters() {
        updateFilterChipSummary('exportActiveFilterCount', 'exportActiveFilterChips', [
            { id: 'exportDept', value: $('exportDept') && $('exportDept').value },
            { id: 'exportLevel', value: $('exportLevel') && $('exportLevel').value },
            { id: 'exportArea', value: $('exportArea') && $('exportArea').value }
        ]);
    }

    function flashUpdated(id) {
        var el = $(id);
        if (!el) return;
        el.classList.remove('updated');
        void el.offsetWidth;
        el.classList.add('updated');
    }

    function getAttQueryParams() {
        var emp = $('attEmployees').value.trim();
        var from = $('attFrom').value;
        var to = $('attTo').value;
        if (!from) { showError(currentLang === 'ar' ? 'يرجى اختيار تاريخ البداية' : 'Please select start date'); return null; }
        if (!to) { showError(currentLang === 'ar' ? 'يرجى اختيار تاريخ النهاية' : 'Please select end date'); return null; }
        var params = 'from=' + encodeURIComponent(from) + '&to=' + encodeURIComponent(to);
        if (emp) params += '&employees=' + encodeURIComponent(emp);
        var matchMode = $('attMatchMode') ? $('attMatchMode').value : 'contains';
        if (matchMode) params += '&matchMode=' + encodeURIComponent(matchMode);
        if ($('attDepartment').value) params += '&department=' + encodeURIComponent($('attDepartment').value);
        if ($('attLevel').value) params += '&level=' + encodeURIComponent($('attLevel').value);
        if ($('attArea').value) params += '&area=' + encodeURIComponent($('attArea').value);
        if ($('attStatus').value) params += '&status=' + encodeURIComponent($('attStatus').value);
        var schedule = selectedScheduleRange();
        if (schedule) {
            params += '&scheduleStart=' + encodeURIComponent(schedule.start);
            params += '&scheduleEnd=' + encodeURIComponent(schedule.end);
        }
        updateAttendanceActiveFilters();
        return params;
    }

    function clearSectionResults(tabName) {
        var sections = {
            attendance: ['attResults', 'attResultsContent'],
            employees: ['empResults', 'empResultsContent'],
            balances: ['balResults', 'balResultsContent'],
            leave: ['leaveResults', 'leaveResultsContent'],
            export: ['exportPreview', 'exportPreviewContent', 'exportResults', 'exportResultsContent'],
            daily: ['dailyResults', 'dailyResultsContent'],
            admin: ['adminResults', 'adminResultsContent']
        };
        var ids = sections[tabName] || [];
        ids.forEach(function (id) {
            var element = $(id);
            if (!element) return;
            if (id.endsWith('Content')) element.replaceChildren();
            else element.style.display = 'none';
        });

        if (tabName === 'attendance') {
            lastAttendanceRecords = null;
            attendancePage = 1;
            attendanceTotalCount = 0;
            attendanceTotalPages = 1;
            if (attendanceRequestController) attendanceRequestController.abort();
            attendanceRequestController = null;
            resetAttendanceFilters();
            hide('attPagination');
            hide('attExportActions');
            $('attPageSummary').textContent = '';
            hide('attRecalculationResult');
            $('attRecalculationResult').textContent = '';
        }
        if (tabName === 'employees') {
            lastEmployeeRows = null;
            $('empSearch').value = '';
            $('empDepartment').value = '';
            $('empLevel').value = '';
            $('empArea').value = '';
            $('empStatus').value = '';
            $('employeesUploadFile').value = '';
            updateEmployeeActiveFilters();
        }
        if (tabName === 'balances') {
            lastBalanceRows = null;
            $('balFinNo').value = '';
            $('balancesUploadFile').value = '';
        }
        if (tabName === 'leave') {
            lastLeaveTransactions = null;
            $('leaveFinNo').value = '';
            $('leaveTypeId').value = '';
            $('leaveFrom').value = '';
            $('leaveTo').value = '';
            $('leaveDays').value = '';
            $('leaveReason').value = '';
            $('leavesUploadFile').value = '';
            $('leaveResultFilter').value = '';
            $('leaveResultSort').value = 'date-desc';
            if ($('editLeaveDialog').open) $('editLeaveDialog').close();
        }
        if (tabName === 'export') {
            lastMonthlyPreview = null;
            lastMonthlyInfo = null;
            $('exportYear').value = new Date().getFullYear();
            $('exportMonth').value = String(new Date().getMonth() + 1);
            $('exportDept').value = '';
            $('exportLevel').value = '';
            $('exportArea').value = '';
            hide('monthlyExportActions');
            updateExportActiveFilters();
        }
if (tabName === 'daily') {
            lastDailyRows = null;
            $('dailyDate').valueAsDate = new Date();
            hide('dailyExportActions');
        }
        if (tabName === 'reports') {
            lastOvertimeRecords = null;
            lastWageRows = null;
            hide('reportsExportActions');
            hide('wageExportActions');
            if (wageRequestController) wageRequestController.abort();
            wageRequestController = null;
        }
        if (tabName === 'admin') {
            lastAdminUsers = null;
            lastAdminDays = null;
            lastPermissionsAll = null;
            $('adminUsername').value = '';
            $('adminDisplayNameAr').value = '';
            $('adminDisplayNameEn').value = '';
            $('adminPassword').value = '';
            $('adminRole').value = 'Admin';
            $('adminPermissions').replaceChildren();
            $('calendarDate').value = '';
            $('calendarDayType').value = 'Weekly Rest';
            $('calendarNotes').value = '';
            $('scheduleFinancialNumbers').value = '';
            $('scheduleStart').value = '';
            $('scheduleEnd').value = '';
            $('scheduleUpdateResult').textContent = '';
            $('resetPasswordValue').value = '';
            $('resetPasswordConfirm').value = '';
        }
        updateFileLabels();
    }

    function clearQueryFilters(queryName) {
        hideError();

        if (queryName === 'attendance') {
            resetAttendanceFilters();
            lastAttendanceRecords = null;
            attendancePage = 1;
            attendanceTotalCount = 0;
            attendanceTotalPages = 1;
            if (attendanceRequestController) attendanceRequestController.abort();
            attendanceRequestController = null;
            hide('attResults');
            hide('attPagination');
            clear($('attResultsContent'));
            $('attExportActions').style.display = 'none';
            $('attPageSummary').textContent = '';
            hide('attRecalculationResult');
            $('attRecalculationResult').textContent = '';
        }

        if (queryName === 'employees') {
            $('empSearch').value = '';
            $('empDepartment').value = '';
            $('empLevel').value = '';
            $('empArea').value = '';
            $('empStatus').value = '';
            lastEmployeeRows = null;
            hide('empResults');
            clear($('empResultsContent'));
            updateEmployeeActiveFilters();
        }

        if (queryName === 'balances') {
            $('balFinNo').value = '';
            lastBalanceRows = null;
            hide('balResults');
            clear($('balResultsContent'));
        }

        if (queryName === 'leave') {
            $('leaveFinNo').value = '';
            $('leaveTypeId').value = '';
            $('leaveFrom').value = '';
            $('leaveTo').value = '';
            $('leaveDays').value = '';
            $('leaveReason').value = '';
            $('leaveResultFilter').value = '';
            $('leaveResultSort').value = 'date-desc';
            lastLeaveTransactions = null;
            hide('leaveResults');
            clear($('leaveResultsContent'));
            $('leaveExportActions').style.display = 'none';
        }

        if (queryName === 'export') {
            $('exportYear').value = new Date().getFullYear();
            $('exportMonth').value = String(new Date().getMonth() + 1);
            $('exportDept').value = '';
            $('exportLevel').value = '';
            $('exportArea').value = '';
            lastMonthlyPreview = null;
            lastMonthlyInfo = null;
            hide('exportPreview');
            hide('exportResults');
            hide('monthlyExportActions');
            clear($('exportPreviewContent'));
            clear($('exportResultsContent'));
            updateExportActiveFilters();
        }

        if (queryName === 'daily') {
            $('dailyDate').valueAsDate = new Date();
            lastDailyRows = null;
            hide('dailyResults');
            hide('dailyExportActions');
            clear($('dailyResultsContent'));
        }

        if (queryName === 'reports') {
            var nowRep = new Date();
            $('reportsFrom').value = new Date(nowRep.getFullYear(), nowRep.getMonth(), 1).toISOString().split('T')[0];
            $('reportsTo').value = new Date(nowRep.getFullYear(), nowRep.getMonth() + 1, 0).toISOString().split('T')[0];
            lastOvertimeRecords = null;
            hide('reportsResults');
            hide('reportsExportActions');
            clear($('reportsResultsContent'));
        }

        if (queryName === 'wage') {
            var nowWage = new Date();
            $('wageYear').value = nowWage.getFullYear();
            renderWageMonthOptions();
            $('wageMonth').value = String(nowWage.getMonth() + 1);
            lastWageRows = null;
            hide('wageResults');
            hide('wageExportActions');
            clear($('wageResultsContent'));
        }
    }

    document.querySelectorAll('[data-clear-query]').forEach(function (button) {
        button.addEventListener('click', function () {
            clearQueryFilters(this.getAttribute('data-clear-query'));
        });
    });

function activateTab(tabName, clearPrevious) {
        if (tabName === 'daily') tabName = 'reports';
        var target = document.querySelector('.tab[data-tab="' + tabName + '"]');
        if (!target) return;
        var permissions = target.getAttribute('data-permission');
        if (permissions && !hasAnyPermission(permissions)) {
            tabName = 'attendance';
            target = document.querySelector('.tab[data-tab="attendance"]');
        }

        var active = document.querySelector('.tab.active');
        var previousName = active ? active.getAttribute('data-tab') : null;
        if (clearPrevious !== false && previousName && previousName !== tabName) {
            clearSectionResults(previousName);
        }

        document.querySelectorAll('.tab').forEach(function (tab) {
            var isActiveTab = tab.getAttribute('data-tab') === tabName;
            tab.classList.toggle('active', isActiveTab);
            if (isActiveTab) tab.setAttribute('aria-current', 'page');
            else tab.removeAttribute('aria-current');
        });
        document.querySelectorAll('.tab-content').forEach(function (content) {
            content.classList.remove('active');
        });
        var contentId = tabName === 'attendance' ? 'tab-attendance' : 'tab-' + tabName;
        $(contentId).classList.add('active');
        sessionStorage.setItem('attendance.activeTab', tabName);
        updateWorkspaceBrief();
        target.scrollIntoView({
            behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth',
            block: 'nearest',
            inline: 'nearest'
        });
if (tabName === 'admin' && currentUser) loadAdminData();
        if (tabName === 'leave' && currentUser) loadApprovalQueues();
    }

    function restoreActiveTab() {
        var savedTab = sessionStorage.getItem('attendance.activeTab') || 'attendance';
        activateTab(savedTab, false);
    }

    document.querySelectorAll('.tab').forEach(function (tab) {
        tab.addEventListener('click', function () {
            activateTab(this.dataset.tab, true);
        });
    });

var today = new Date();
    $('attFrom').valueAsDate = today;
    $('attTo').valueAsDate = today;
    $('dailyDate').valueAsDate = today;
    $('exportYear').value = today.getFullYear();
    $('exportMonth').value = String(today.getMonth() + 1);
    $('leaveDays').value = '';
    // Reports tab - current month defaults
    $('reportsFrom').value = new Date(today.getFullYear(), today.getMonth(), 1).toISOString().split('T')[0];
    $('reportsTo').value = new Date(today.getFullYear(), today.getMonth() + 1, 0).toISOString().split('T')[0];
    $('wageYear').value = today.getFullYear();
    renderWageMonthOptions();
    renderAttendanceFilterOptions();
    ['attEmployees', 'attMatchMode', 'attDepartment', 'attStatus', 'attLevel', 'attArea', 'attSchedule'].forEach(function (id) {
        var el = $(id);
        if (!el) return;
        el.addEventListener(el.tagName === 'INPUT' ? 'input' : 'change', updateAttendanceActiveFilters);
    });
    renderEmployeeFilterOptions();
    ['empSearch', 'empDepartment', 'empLevel', 'empArea', 'empStatus'].forEach(function (id) {
        var el = $(id);
        if (!el) return;
        el.addEventListener(el.tagName === 'INPUT' ? 'input' : 'change', updateEmployeeActiveFilters);
    });
renderExportFilterOptions();
    ['exportYear', 'exportMonth', 'exportDept', 'exportLevel', 'exportArea'].forEach(function (id) {
        var el = $(id);
        if (!el) return;
        el.addEventListener(el.tagName === 'INPUT' ? 'input' : 'change', updateExportActiveFilters);
    });

    var autoTimers = {};
    function scheduleAuto(section, fn, delay) {
        clearTimeout(autoTimers[section]);
        autoTimers[section] = setTimeout(function () {
            if (isResultsVisible(section)) fn();
        }, delay || 450);
    }
    function isResultsVisible(section) {
        var map = {
            attendance: 'attResults',
            employees: 'empResults',
            balances: 'balResults',
            leave: 'leaveResults',
            monthly: 'exportPreview',
            daily: 'dailyResults',
            reports: 'reportsResults'
        };
        var el = $(map[section]);
        return el && el.style.display !== 'none';
    }

    if ($('attEmployees')) {
        $('attEmployees').addEventListener('input', function () { scheduleAuto('attendance', function () { fetchAttendance(true); }, 550); });
    }
    ['attDepartment', 'attStatus', 'attLevel', 'attArea', 'attSchedule'].forEach(function (id) {
        var el = $(id);
        if (el) el.addEventListener('change', function () { scheduleAuto('attendance', function () { fetchAttendance(true); }, 400); });
    });
    if ($('empSearch')) {
        $('empSearch').addEventListener('input', function () { scheduleAuto('employees', function () { $('searchEmpBtn').click(); }, 550); });
    }
    ['empDepartment', 'empLevel', 'empArea', 'empStatus'].forEach(function (id) {
        var el = $(id);
        if (el) el.addEventListener('change', function () { scheduleAuto('employees', function () { $('searchEmpBtn').click(); }, 350); });
    });
    if ($('balFinNo')) {
        $('balFinNo').addEventListener('input', function () { scheduleAuto('balances', function () { $('fetchBalBtn').click(); }, 550); });
    }
    if ($('leaveFinNo')) {
        $('leaveFinNo').addEventListener('input', function () { scheduleAuto('leave', function () { $('fetchLeaveTransBtn').click(); }, 550); });
    }
    ['leaveTypeId', 'leaveFrom', 'leaveTo'].forEach(function (id) {
        var el = $(id);
        if (el) el.addEventListener('change', function () { scheduleAuto('leave', function () { $('fetchLeaveTransBtn').click(); }, 400); });
    });
    ['exportYear', 'exportMonth', 'exportDept', 'exportLevel', 'exportArea'].forEach(function (id) {
        var el = $(id);
        if (el) el.addEventListener('change', function () { scheduleAuto('monthly', function () { $('exportPreviewBtn').click(); }, 400); });
    });
if ($('dailyDate')) {
        $('dailyDate').addEventListener('change', function () { scheduleAuto('daily', function () { $('fetchDailyBtn').click(); }, 300); });
    }
    // Reports tab event listeners
    if ($('fetchReportsBtn')) {
        $('fetchReportsBtn').addEventListener('click', function () { fetchOvertimeReport(); });
    }
    ['reportsFrom', 'reportsTo'].forEach(function (id) {
        var el = $(id);
        if (el) el.addEventListener('change', function () { fetchOvertimeReport(); });
    });
    if ($('reportsExportExcelBtn')) {
        $('reportsExportExcelBtn').addEventListener('click', function () {
            var from = $('reportsFrom').value, to = $('reportsTo').value;
            if (!from || !to) return;
            window.open('/api/tracking/top-management/overtime/export/excel?from=' + encodeURIComponent(from) + '&to=' + encodeURIComponent(to), '_blank');
        });
    }
    if ($('reportsExportPdfBtn')) {
        $('reportsExportPdfBtn').addEventListener('click', function () {
            var from = $('reportsFrom').value, to = $('reportsTo').value;
            if (!from || !to) return;
            window.open('/api/tracking/top-management/overtime/export/pdf?from=' + encodeURIComponent(from) + '&to=' + encodeURIComponent(to), '_blank');
        });
    }
    // Daily-wage report listeners
    if ($('fetchWageBtn')) {
        $('fetchWageBtn').addEventListener('click', function () { fetchWageReport(); });
    }
    ['wageYear', 'wageMonth'].forEach(function (id) {
        var el = $(id);
        if (el) el.addEventListener('change', function () { fetchWageReport(); });
    });
    if ($('wageExportExcelBtn')) {
        $('wageExportExcelBtn').addEventListener('click', function () {
            var params = getWageQueryParams();
            if (!params) return;
            window.open('/api/tracking/daily-wage/present-days/export/excel?' + params + '&lang=' + currentLang, '_blank');
        });
    }
    if ($('wageExportPdfBtn')) {
        $('wageExportPdfBtn').addEventListener('click', function () {
            var params = getWageQueryParams();
            if (!params) return;
            window.open('/api/tracking/daily-wage/present-days/export/pdf?' + params + '&lang=' + currentLang, '_blank');
        });
    }

function updateLeaveDays(fromId, toId, daysId) {
        fromId = fromId || 'leaveFrom';
        toId = toId || 'leaveTo';
        daysId = daysId || 'leaveDays';
        var from = $(fromId).value;
        var to = $(toId).value;
        var requestId = ++leaveDaysRequestId;
        if (!from || !to) {
            $(daysId).value = '';
            return Promise.resolve(0);
        }

        $(daysId).value = '';
        return fetch('/api/leave/day-count?fromDate=' + encodeURIComponent(from) + '&toDate=' + encodeURIComponent(to), { cache: 'no-store' })
            .then(function (r) {
                if (!r.ok) throw new Error(currentLang === 'ar' ? 'تعذر حساب أيام الإجازة' : 'Could not calculate leave days');
                return r.json();
            })
            .then(function (data) {
                var days = data && data.daysCount ? data.daysCount : 0;
                if (requestId === leaveDaysRequestId) $(daysId).value = days;
                return days;
            })
            .catch(function () {
                if (requestId === leaveDaysRequestId) $(daysId).value = 0;
                return 0;
            });
    }

    $('leaveFrom').addEventListener('change', function () { updateLeaveDays('leaveFrom', 'leaveTo', 'leaveDays'); });
    $('leaveTo').addEventListener('change', function () { updateLeaveDays('leaveFrom', 'leaveTo', 'leaveDays'); });
    updateLeaveDays('leaveFrom', 'leaveTo', 'leaveDays');

    if ($('reqLeaveFrom')) {
        $('reqLeaveFrom').addEventListener('change', function () { updateLeaveDays('reqLeaveFrom', 'reqLeaveTo', 'reqLeaveDays'); });
        $('reqLeaveTo').addEventListener('change', function () { updateLeaveDays('reqLeaveFrom', 'reqLeaveTo', 'reqLeaveDays'); });
    }

    if ($('requestLeaveBtn')) {
        $('requestLeaveBtn').addEventListener('click', function () {
            var leaveTypeId = $('reqLeaveTypeId').value;
            var fromDate = $('reqLeaveFrom').value;
            var toDate = $('reqLeaveTo').value;
            var reason = $('reqLeaveReason').value.trim();

            if (!leaveTypeId) { showError(currentLang === 'ar' ? 'يرجى اختيار نوع الإجازة' : 'Please select leave type'); return; }
            if (!fromDate || !toDate) { showError(currentLang === 'ar' ? 'يرجى اختيار تاريخ البداية والنهاية' : 'Please select start and end dates'); return; }

            showLoading();
            hideError();

            updateLeaveDays('reqLeaveFrom', 'reqLeaveTo', 'reqLeaveDays')
                .then(function (days) {
                    if (parseFloat(days) <= 0) {
                        throw new Error(currentLang === 'ar' ? 'الفترة المحددة لا تحتوي على أيام إجازة فعلية' : 'The selected period has no actual leave days');
                    }
                    return fetch('/api/leave/request', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({
                            leaveTypeId: parseInt(leaveTypeId, 10),
                            fromDate: fromDate,
                            toDate: toDate,
                            reason: reason || null
                        })
                    });
                })
                .then(function (r) {
                    if (!r.ok) return r.json().then(function (e) { throw new Error(e.error); });
                    return r.json();
                })
                .then(function () {
                    hideLoading();
                    showSuccess(i18n[currentLang].requestLeaveSent);
                    $('reqLeaveTypeId').value = '';
                    $('reqLeaveFrom').value = '';
                    $('reqLeaveTo').value = '';
                    $('reqLeaveDays').value = '';
                    $('reqLeaveReason').value = '';
                    loadApprovalQueues();
                    if ($('leaveResults') && $('leaveResults').style.display !== 'none') $('fetchLeaveTransBtn').click();
                })
                .catch(function (err) {
                    hideLoading();
                    showError(err.message);
                });
        });
    }

    document.querySelectorAll('.choice-btn').forEach(function (btn) {
        btn.addEventListener('click', function () {
            document.querySelectorAll('.choice-btn').forEach(function (b) { b.classList.remove('active'); });
            this.classList.add('active');
            if (this.dataset.mode === 'pc') {
                document.querySelector('[data-tab="export"]').click();
            }
        });
    });

    function loadLeaveTypes() {
        if (leaveTypesCache.length) {
            renderLeaveTypeOptions();
            return Promise.resolve(leaveTypesCache);
        }
        if (leaveTypesRequest) return leaveTypesRequest;

        leaveTypesRequest = fetch('/api/leave/types', {
            cache: 'no-store',
            credentials: 'same-origin'
        }).then(function (response) {
            if (!response.ok) throw new Error('Could not load leave types');
            return response.json();
        }).then(function (types) {
            leaveTypesCache = types || [];
            renderLeaveTypeOptions();
            return leaveTypesCache;
        }).catch(function (error) {
            leaveTypesRequest = null;
            throw error;
        });
        return leaveTypesRequest;
    }

    function updateAttendancePagination() {
        var pagination = $('attPagination');
        if (!pagination || attendanceTotalCount === 0) {
            hide('attPagination');
            return;
        }

        attendanceTotalPages = Math.max(1, attendanceTotalPages);
        attendancePage = Math.min(Math.max(1, attendancePage), attendanceTotalPages);
        $('attPageSummary').textContent = currentLang === 'ar'
            ? attendanceTotalCount + ' ' + i18n.ar.totalRecords + ' • صفحة ' + attendancePage + ' من ' + attendanceTotalPages
            : attendanceTotalCount + ' ' + i18n.en.totalRecords + ' • Page ' + attendancePage + ' of ' + attendanceTotalPages;
        $('attPrevPage').disabled = attendancePage <= 1;
        $('attNextPage').disabled = attendancePage >= attendanceTotalPages;
        $('attPageSize').value = String(attendancePageSize);
        pagination.style.display = 'flex';
    }

    function renderAttendance(data) {
        var el = $('attResultsContent');
        clear(el);

        if (!data || data.length === 0) {
            el.innerHTML = emptyState(
                currentLang === 'ar' ? 'لا توجد بيانات ضمن الفترة المحددة' : 'No records found for the selected range',
                currentLang === 'ar' ? 'جرّب توسيع الفترة أو تغيير أرقام الموظفين.' : 'Try a wider date range or different employee numbers.'
            );
            show('attResults');
            hide('attExportActions');
            hide('attPagination');
            flashUpdated('attResults');
            return;
        }

        var tableLabel = i18n[currentLang].attendanceResults;
        var html = '<p class="table-scroll-hint">' + i18n[currentLang].scrollTableHint + '</p>' +
            '<div class="table-scroll attendance-table-scroll" tabindex="0" role="region" aria-label="' + tableLabel + '">' +
            '<table><thead><tr><th>#</th><th>' + (currentLang === 'ar' ? 'الرقم المالي' : 'Financial No') + '</th><th>' + (currentLang === 'ar' ? 'الاسم' : 'Name') + '</th><th>' + (currentLang === 'ar' ? 'الحالة' : 'Status') + '</th><th>' + (currentLang === 'ar' ? 'اليوم' : 'Day') + '</th><th>' + (currentLang === 'ar' ? 'التاريخ' : 'Date') + '</th><th>' + (currentLang === 'ar' ? 'الحضور' : 'Check In') + '</th><th>' + (currentLang === 'ar' ? 'الانصراف' : 'Check Out') + '</th><th>' + (currentLang === 'ar' ? 'المدة' : 'Duration') + '</th><th>' + (currentLang === 'ar' ? 'الموعد' : 'Schedule') + '</th><th>' + (currentLang === 'ar' ? 'دقائق التأخير' : 'Late minutes') + '</th><th>' + (currentLang === 'ar' ? 'المتبقي من 30 دقيقة' : 'Remaining of 30 min.') + '</th></tr></thead><tbody>';
        data.forEach(function (r, i) {
            var duration = '-';
            if (r.firstPunch && r.lastPunch) {
                var f = new Date(r.firstPunch);
                var l = new Date(r.lastPunch);
                duration = fmtDuration(Math.round((l - f) / 60000));
            }
            html += '<tr>' +
                '<td>' + (((attendancePage - 1) * attendancePageSize) + i + 1) + '</td>' +
                '<td>' + r.employeeFinancialNo + '</td>' +
                '<td class="name-cell">' + r.employeeName + '</td>' +
                '<td>' + badge(r.status) + '</td>' +
                '<td class="day-name">' + fmtDayName(r.dateDisplay) + '</td>' +
                '<td>' + fmtDateLocal(r.dateDisplay) + '</td>' +
                '<td class="time-cell">' + fmtTime(r.firstPunch) + '</td>' +
                '<td class="time-cell">' + fmtTime(r.lastPunch) + '</td>' +
                '<td class="duration">' + duration + '</td>' +
                '<td class="schedule-cell">' + fmtSchedule(r.scheduledStart) + ' - ' + fmtSchedule(r.scheduledEnd) + '</td>' +
                '<td>' + (r.lateMinutes || 0) + '</td>' +
                '<td><strong>' + (r.remainingLateMinutes || 0) + '</strong></td>' +
                '</tr>';
        });
        html += '</tbody></table></div>';
        el.innerHTML = html;
        show('attResults');
        show('attExportActions');
        updateAttendancePagination();
        flashUpdated('attResults');
    }

    var lastOvertimeRecords = null;
    var lastWageRows = null;
    var wageRequestController = null;

    function fetchOvertimeReport() {
        var from = $('reportsFrom').value;
        var to = $('reportsTo').value;
        if (!from || !to) return;
        var params = 'from=' + encodeURIComponent(from) + '&to=' + encodeURIComponent(to);

        if (overtimeRequestController) overtimeRequestController.abort();
        overtimeRequestController = new AbortController();
        var activeController = overtimeRequestController;

        showLoading();
        hideError();
        hide('reportsResults');
        hide('reportsExportActions');

        fetch('/api/tracking/top-management/overtime?' + params + '&_=' + Date.now(), {
            cache: 'no-store',
            signal: activeController.signal
        })
            .then(function (r) { if (!r.ok) throw new Error(currentLang === 'ar' ? 'فشل التحميل' : 'Failed to load'); return r.json(); })
            .then(function (data) {
                if (activeController !== overtimeRequestController) return;
                hideLoading();
                lastOvertimeRecords = Array.isArray(data) ? data : (data.items || []);
                renderOvertimeReport(lastOvertimeRecords);
                show('reportsResults');
                show('reportsExportActions');
                flashUpdated('reportsResults');
                overtimeRequestController = null;
            })
            .catch(function (err) {
                if (err.name === 'AbortError') {
                    if (!overtimeRequestController || activeController === overtimeRequestController) hideLoading();
                    return;
                }
                hideLoading();
                showError(err.message);
                overtimeRequestController = null;
            });
    }

    function renderOvertimeReport(data) {
        var el = $('reportsResultsContent');
        clear(el);

        if (!data || data.length === 0) {
            el.innerHTML = emptyState(
                currentLang === 'ar' ? 'لا توجد بيانات ضمن الفترة المحددة' : 'No records found for the selected range',
                currentLang === 'ar' ? 'جرّب توسيع الفترة.' : 'Try a wider date range.'
            );
            show('reportsResults');
            hide('reportsExportActions');
            flashUpdated('reportsResults');
            return;
        }

        var tableLabel = i18n[currentLang].overtimeResults;
        var html = '<p class="table-scroll-hint">' + i18n[currentLang].scrollTableHint + '</p>' +
            '<div class="table-scroll attendance-table-scroll" tabindex="0" role="region" aria-label="' + tableLabel + '">' +
            '<table><thead><tr>' +
            '<th>#</th>' +
            '<th>' + (currentLang === 'ar' ? 'الرقم المالي' : 'Financial No') + '</th>' +
            '<th>' + (currentLang === 'ar' ? 'الاسم' : 'Name') + '</th>' +
            '<th>' + (currentLang === 'ar' ? 'المسمى الوظيفي' : 'Job Title') + '</th>' +
            '<th>' + (currentLang === 'ar' ? 'الإدارة' : 'Department') + '</th>' +
            '<th>' + (currentLang === 'ar' ? 'عدد أيام العمل الإضافي' : 'Overtime Days') + '</th>' +
            '<th>' + (currentLang === 'ar' ? 'إجمالي ساعات العمل الإضافي' : 'Total Overtime (H:MM)') + '</th>' +
            '<th>' + (currentLang === 'ar' ? 'إجمالي دقائق العمل الإضافي' : 'Total Overtime Minutes') + '</th>' +
            '</tr></thead><tbody>';

        data.forEach(function (r, i) {
            html += '<tr>' +
                '<td>' + (i + 1) + '</td>' +
                '<td>' + r.financialNo + '</td>' +
                '<td class="name-cell">' + r.name + '</td>' +
                '<td>' + (r.jobTitle || '-') + '</td>' +
                '<td>' + (r.department || '-') + '</td>' +
                '<td>' + (r.overtimeDays || 0) + '</td>' +
                '<td class="duration">' + (r.overtimeFormatted || '-') + '</td>' +
                '<td>' + (r.overtimeMinutes || 0) + '</td>' +
                '</tr>';
        });

        html += '</tbody></table></div>';
        el.innerHTML = html;
        show('reportsResults');
        show('reportsExportActions');
        flashUpdated('reportsResults');
    }

    function renderEmployees(data) {
        var el = $('empResultsContent');
        clear(el);

        if (!data || data.length === 0) {
            hide('empExportActions');
            el.innerHTML = emptyState(currentLang === 'ar' ? 'لا توجد نتائج' : 'No results found');
            show('empResults');
            flashUpdated('empResults');
            return;
        }

        var html = tableScrollOpen(i18n[currentLang].searchResults) + '<table><thead><tr><th>#</th><th>' + (currentLang === 'ar' ? 'الرقم المالي' : 'Financial No') + '</th><th>' + (currentLang === 'ar' ? 'الاسم' : 'Name') + '</th><th>' + (currentLang === 'ar' ? 'الوظيفة' : 'Job Title') + '</th><th>' + (currentLang === 'ar' ? 'المستوى' : 'Level') + '</th><th>' + (currentLang === 'ar' ? 'الإدارة' : 'Department') + '</th><th>' + (currentLang === 'ar' ? 'الموعد المخصص' : 'Custom schedule') + '</th></tr></thead><tbody>';
        data.forEach(function (r, i) {
            html += '<tr>' +
                '<td>' + (i + 1) + '</td>' +
                '<td>' + r.financialNo + '</td>' +
                '<td style="text-align:right">' + r.name + '</td>' +
                '<td>' + (r.jobTitle || '-') + '</td>' +
                '<td>' + (r.level || '-') + '</td>' +
                '<td>' + (r.department || '-') + '</td>' +
                '<td>' + (r.scheduleStart && r.scheduleEnd ? fmtSchedule(r.scheduleStart) + ' - ' + fmtSchedule(r.scheduleEnd) : '-') + '</td>' +
                '</tr>';
        });
        html += '</tbody></table></div>';
        el.innerHTML = html;
        show('empResults');
        $('empExportActions').style.display = 'flex';
        flashUpdated('empResults');
    }

    function renderBalances(data) {
        var el = $('balResultsContent');
        clear(el);

        if (!data || data.length === 0) {
            hide('balExportActions');
            el.innerHTML = emptyState(currentLang === 'ar' ? 'لا توجد أرصدة' : 'No balances found');
            show('balResults');
            flashUpdated('balResults');
            return;
        }

        var html = tableScrollOpen(i18n[currentLang].balancesTitle) + '<table><thead><tr><th>' + (currentLang === 'ar' ? 'السنة' : 'Year') + '</th><th>' + (currentLang === 'ar' ? 'الرقم المالي' : 'Financial No') + '</th><th>' + (currentLang === 'ar' ? 'الاسم' : 'Name') + '</th><th>' + (currentLang === 'ar' ? 'اعتيادى' : 'Regular') + '</th><th>' + (currentLang === 'ar' ? 'عارضه' : 'Casual') + '</th><th>' + (currentLang === 'ar' ? 'بدل راحه' : 'Rest Allow.') + '</th><th>' + (currentLang === 'ar' ? 'بدل عطله' : 'Holiday Allow.') + '</th></tr></thead><tbody>';
        data.forEach(function (b) {
            html += '<tr><td>' + b.year + '</td><td>' + (b.employeeFinancialNo || (b.employee ? b.employee.financialNo : '')) + '</td><td>' + (b.employee ? b.employee.name : '') + '</td><td>' + b.regularLeave + '</td><td>' + b.casualLeave + '</td><td>' + b.restAllowance + '</td><td>' + b.holidayAllowance + '</td></tr>';
        });
        html += '</tbody></table></div>';
        el.innerHTML = html;
        show('balResults');
        $('balExportActions').style.display = 'flex';
        flashUpdated('balResults');
    }

    function dailyNotes(row) {
        var notes = [];
        if (row.missingYesterdayCheckout) {
            notes.push(currentLang === 'ar' ? 'لم يسجل انصراف اليوم السابق' : 'Previous-day check-out is missing');
        }
        if (row.missingTodayCheckIn) {
            notes.push(currentLang === 'ar' ? 'لم يسجل حضور اليوم' : "Today's check-in is missing");
        }
        return notes.join(currentLang === 'ar' ? ' - ' : '; ');
    }

    function renderDaily(data) {
        var el = $('dailyResultsContent');
        clear(el);
        if (!data || data.length === 0) {
            hide('dailyExportActions');
            el.innerHTML = emptyState(currentLang === 'ar' ? 'لا توجد نتائج للتاريخ المحدد' : 'No results for the selected date');
            show('dailyResults');
            flashUpdated('dailyResults');
            return;
        }

        var headers = currentLang === 'ar'
            ? ['#', 'الرقم المالي', 'الاسم', 'المسمى الوظيفي', 'الإدارة', 'انصراف اليوم السابق', 'حضور اليوم', 'ملاحظات']
            : ['#', 'Financial No', 'Name', 'Job Title', 'Department', 'Previous Day Check Out', 'Today Check In', 'Notes'];
        var html = tableScrollOpen(i18n[currentLang].dailyResults) + '<table><thead><tr>';
        headers.forEach(function (header) { html += '<th>' + header + '</th>'; });
        html += '</tr></thead><tbody>';
        data.forEach(function (row, index) {
            html += '<tr>' +
                '<td>' + (index + 1) + '</td>' +
                '<td>' + row.financialNo + '</td>' +
                '<td class="name-cell">' + (row.name || '-') + '</td>' +
                '<td>' + (row.jobTitle || '-') + '</td>' +
                '<td>' + (row.department || '-') + '</td>' +
                '<td class="time-cell">' + fmtTime(row.yesterdayLastPunch) + '</td>' +
                '<td class="time-cell">' + fmtTime(row.todayFirstPunch) + '</td>' +
                '<td>' + (dailyNotes(row) || '-') + '</td>' +
                '</tr>';
        });
        html += '</tbody></table></div>';
        el.innerHTML = html;
        show('dailyResults');
        $('dailyExportActions').style.display = 'flex';
        flashUpdated('dailyResults');
    }

    function renderWageMonthOptions() {
        var sel = $('wageMonth');
        if (!sel) return;
        var selected = sel.value || String(new Date().getMonth() + 1);
        var names = monthNames[currentLang] || monthNames.ar;
        sel.innerHTML = '';
        for (var m = 1; m <= 12; m++) {
            var opt = document.createElement('option');
            opt.value = String(m);
            opt.textContent = names[m - 1];
            sel.appendChild(opt);
        }
        sel.value = selected;
    }

    function getWageQueryParams() {
        var year = $('wageYear').value, month = $('wageMonth').value;
        if (!year || !month) {
            showError(currentLang === 'ar' ? 'يرجى اختيار السنة والشهر' : 'Please select year and month');
            return null;
        }
        return 'year=' + encodeURIComponent(year) + '&month=' + encodeURIComponent(month);
    }

    function fetchWageReport() {
        var params = getWageQueryParams();
        if (!params) return;

        if (wageRequestController) wageRequestController.abort();
        wageRequestController = new AbortController();
        var activeController = wageRequestController;

        showLoading();
        hideError();
        hide('wageResults');
        hide('wageExportActions');

        fetch('/api/tracking/daily-wage/present-days?' + params + '&_=' + Date.now(), {
            cache: 'no-store',
            signal: activeController.signal
        })
            .then(function (r) { if (!r.ok) throw new Error(currentLang === 'ar' ? 'فشل التحميل' : 'Failed to load'); return r.json(); })
            .then(function (data) {
                if (activeController !== wageRequestController) return;
                hideLoading();
                lastWageRows = Array.isArray(data) ? data : [];
                renderWageReport(lastWageRows);
                wageRequestController = null;
            })
            .catch(function (err) {
                if (err.name === 'AbortError') {
                    if (!wageRequestController || activeController === wageRequestController) hideLoading();
                    return;
                }
                hideLoading();
                showError(err.message);
                wageRequestController = null;
            });
    }

    function renderWageReport(data) {
        var el = $('wageResultsContent');
        clear(el);

        if (!data || data.length === 0) {
            el.innerHTML = emptyState(
                currentLang === 'ar' ? 'لا توجد بيانات لهذا الشهر' : 'No data for this month',
                currentLang === 'ar' ? 'جرّب شهرًا آخر.' : 'Try another month.'
            );
            show('wageResults');
            hide('wageExportActions');
            flashUpdated('wageResults');
            return;
        }

        var tableLabel = i18n[currentLang].wageResults;
        var html = '<p class="table-scroll-hint">' + i18n[currentLang].scrollTableHint + '</p>' +
            '<div class="table-scroll attendance-table-scroll" tabindex="0" role="region" aria-label="' + tableLabel + '">' +
            '<table><thead><tr>' +
            '<th>#</th>' +
            '<th>' + (currentLang === 'ar' ? 'الرقم المالي' : 'Financial No') + '</th>' +
            '<th>' + (currentLang === 'ar' ? 'الاسم' : 'Name') + '</th>' +
            '<th>' + (currentLang === 'ar' ? 'المسمى الوظيفي' : 'Job Title') + '</th>' +
            '<th>' + (currentLang === 'ar' ? 'الإدارة' : 'Department') + '</th>' +
            '<th>' + (currentLang === 'ar' ? 'أيام الحضور' : 'Present Days') + '</th>' +
            '</tr></thead><tbody>';

        data.forEach(function (r, i) {
            html += '<tr>' +
                '<td>' + (i + 1) + '</td>' +
                '<td>' + r.financialNo + '</td>' +
                '<td class="name-cell">' + r.name + '</td>' +
                '<td>' + (r.jobTitle || '-') + '</td>' +
                '<td>' + (r.department || '-') + '</td>' +
                '<td><strong>' + (r.presentDays || 0) + '</strong></td>' +
                '</tr>';
        });

        html += '</tbody></table></div>';
        el.innerHTML = html;
        show('wageResults');
        show('wageExportActions');
        flashUpdated('wageResults');
    }

    // Attendance Tab
    function fetchAttendance(resetPage) {
        if (resetPage !== false) attendancePage = 1;
        var params = getAttQueryParams();
        if (!params) return;
        params += '&page=' + attendancePage + '&pageSize=' + attendancePageSize;
        if ($('attResultSort')) params += '&sort=' + encodeURIComponent($('attResultSort').value || 'fin-no');

        if (attendanceRequestController) attendanceRequestController.abort();
        attendanceRequestController = new AbortController();
        var activeRequestController = attendanceRequestController;

        showLoading();
        hideError();
        hide('attResults');
        hide('attExportActions');

        fetch('/api/tracking/query?' + params + '&_=' + Date.now(), {
            cache: 'no-store',
            signal: activeRequestController.signal
        })
            .then(function (r) { if (!r.ok) throw new Error(currentLang === 'ar' ? 'فشل التحميل' : 'Failed to load'); return r.json(); })
            .then(function (data) {
                if (activeRequestController !== attendanceRequestController) return;
                hideLoading();
                lastAttendanceRecords = Array.isArray(data) ? data : (data.items || []);
                attendanceTotalCount = Array.isArray(data) ? lastAttendanceRecords.length : (data.totalCount || 0);
                attendancePage = Array.isArray(data) ? 1 : (data.page || attendancePage);
                attendancePageSize = Array.isArray(data) ? Math.max(lastAttendanceRecords.length, 1) : (data.pageSize || attendancePageSize);
                attendanceTotalPages = Array.isArray(data) ? 1 : (data.totalPages || 1);
                renderAttendance(lastAttendanceRecords);
                attendanceRequestController = null;
            })
            .catch(function (err) {
                if (err.name === 'AbortError') {
                    if (!attendanceRequestController || activeRequestController === attendanceRequestController) hideLoading();
                    return;
                }
                hideLoading();
                showError(err.message);
                attendanceRequestController = null;
            });
    }

    $('fetchAttendanceBtn').addEventListener('click', function () {
        fetchAttendance(true);
    });

    $('attPrevPage').addEventListener('click', function () {
        if (attendancePage <= 1) return;
        attendancePage -= 1;
        fetchAttendance(false);
    });

    $('attNextPage').addEventListener('click', function () {
        if (attendancePage >= attendanceTotalPages) return;
        attendancePage += 1;
        fetchAttendance(false);
    });

    $('attPageSize').addEventListener('change', function () {
        attendancePageSize = Number(this.value) || 50;
        attendancePage = 1;
        fetchAttendance(false);
    });

    $('attResultSort').addEventListener('change', function () {
        attendancePage = 1;
        if (lastAttendanceRecords) fetchAttendance(false);
    });

    if ($('attMatchMode')) $('attMatchMode').addEventListener('change', function () {
        attendancePage = 1;
        if (lastAttendanceRecords) fetchAttendance(false);
    });

    $('exportExcelBtn').addEventListener('click', function () {
        var params = getAttQueryParams();
        if (!params) return;
        window.open('/api/tracking/export/excel?' + params + '&_=' + Date.now(), '_blank');
    });

    $('exportPdfBtn').addEventListener('click', function () {
        var params = getAttQueryParams();
        if (!params) return;
        window.open('/api/tracking/export/pdf?' + params + '&lang=' + currentLang + '&_=' + Date.now(), '_blank');
    });

    $('recalculateAttendanceBtn').addEventListener('click', function () {
        var paramsText = getAttQueryParams();
        if (!paramsText || !window.confirm(i18n[currentLang].recalculationConfirm)) return;

        var params = new URLSearchParams(paramsText);
        var request = {
            from: params.get('from'),
            to: params.get('to'),
            employees: params.get('employees'),
            matchMode: params.get('matchMode'),
            department: params.get('department'),
            level: params.get('level'),
            area: params.get('area'),
            scheduleStart: params.get('scheduleStart'),
            scheduleEnd: params.get('scheduleEnd')
        };

        showLoading();
        hideError();
        hide('attRecalculationResult');
        fetch('/api/tracking/recalculate-statuses', {
            method: 'POST',
            credentials: 'same-origin',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(request)
        }).then(readUploadResponse)
            .then(function (result) {
                hideLoading();
                var dict = i18n[currentLang];
                $('attRecalculationResult').textContent = dict.recalculationComplete + ': ' +
                    result.processedCount + ' ' + dict.processedRecords + '، ' +
                    result.updatedCount + ' ' + dict.updatedRecords + '، ' +
                    result.skippedCount + ' ' + dict.skippedRecords + '.';
                show('attRecalculationResult');
                $('fetchAttendanceBtn').click();
            })
            .catch(function (error) {
                hideLoading();
                showError(error.message);
            });
    });

    // Employees Tab
    function getEmployeeQueryParams() {
        var params = new URLSearchParams();
        var q = $('empSearch').value.trim();
        if (q) params.set('search', q);
        if ($('empDepartment').value) params.set('department', $('empDepartment').value);
        if ($('empLevel').value) params.set('level', $('empLevel').value);
        if ($('empArea').value) params.set('area', $('empArea').value);
        if ($('empStatus').value) params.set('status', $('empStatus').value);
        updateEmployeeActiveFilters();
        return params.toString();
    }

    $('searchEmpBtn').addEventListener('click', function () {
        showLoading();
        hideError();
        hide('empResults');

        var params = getEmployeeQueryParams();
        var url = params ? '/api/employees?' + params + '&_=' + Date.now() : '/api/employees?_=' + Date.now();
        fetch(url, { cache: 'no-store', credentials: 'same-origin' })
            .then(function (r) { return r.json(); })
            .then(function (data) {
                hideLoading();
                lastEmployeeRows = data || [];
                renderEmployees(lastEmployeeRows);
            })
            .catch(function (err) {
                hideLoading();
                showError(err.message);
            });
    });

    $('empSearch').addEventListener('keyup', function (e) {
        if (e.key === 'Enter') $('searchEmpBtn').click();
    });

    function openEmployeeExport(format) {
        var params = new URLSearchParams(getEmployeeQueryParams());
        params.set('lang', currentLang);
        params.set('_', Date.now());
        window.open('/api/employees/export/' + format + '?' + params.toString(), '_blank');
    }

    $('exportEmployeesExcelBtn').addEventListener('click', function () { openEmployeeExport('excel'); });
    $('exportEmployeesPdfBtn').addEventListener('click', function () { openEmployeeExport('pdf'); });

// Employees Upload
    $('uploadEmployeesBtn').addEventListener('click', function () {
        performUpload('employeesUploadFile', function () {
            lastEmployeeRows = null;
            hide('empResults');
            $('searchEmpBtn').click();
        });
    });

    // Balances Tab
    $('fetchBalBtn').addEventListener('click', function () {
        var finNo = $('balFinNo').value.trim();

        showLoading();
        hideError();
        hide('balResults');

        var url = finNo ? '/api/tracking/' + encodeURIComponent(finNo) + '/balances' : '/api/tracking/balances';

        fetch(url)
            .then(function (r) { return r.json(); })
            .then(function (data) {
                hideLoading();
                lastBalanceRows = data || [];
                renderBalances(lastBalanceRows);
            })
            .catch(function (err) {
                hideLoading();
                showError(err.message);
            });
    });

    $('balFinNo').addEventListener('keyup', function (e) {
        if (e.key === 'Enter') $('fetchBalBtn').click();
    });

    function openBalanceExport(format) {
        var params = new URLSearchParams();
        var financialNo = $('balFinNo').value.trim();
        if (financialNo) params.set('financialNo', financialNo);
        params.set('lang', currentLang);
        params.set('_', Date.now());
        window.open('/api/tracking/balances/export/' + format + '?' + params.toString(), '_blank');
    }

    $('exportBalancesExcelBtn').addEventListener('click', function () { openBalanceExport('excel'); });
    $('exportBalancesPdfBtn').addEventListener('click', function () { openBalanceExport('pdf'); });

    // Balances Upload
    $('uploadBalancesBtn').addEventListener('click', function () {
        performUpload('balancesUploadFile', function () {
            lastBalanceRows = null;
            hide('balResults');
            $('fetchBalBtn').click();
        });
    });

    // Leave Tab
    $('grantLeaveBtn').addEventListener('click', function () {
        var finNo = $('leaveFinNo').value.trim();
        var leaveTypeId = $('leaveTypeId').value;
        var fromDate = $('leaveFrom').value;
        var toDate = $('leaveTo').value;
        var reason = $('leaveReason').value.trim();

        if (!finNo) { showError(currentLang === 'ar' ? 'يرجى إدخال الرقم المالي' : 'Please enter financial number'); return; }
        if (!leaveTypeId) { showError(currentLang === 'ar' ? 'يرجى اختيار نوع الإجازة' : 'Please select leave type'); return; }
        if (!fromDate || !toDate) { showError(currentLang === 'ar' ? 'يرجى اختيار تاريخ البداية والنهاية' : 'Please select start and end dates'); return; }

        showLoading();
        hideError();
        hide('leaveResults');

        updateLeaveDays()
            .then(function (days) {
                if (parseFloat(days) <= 0) {
                    throw new Error(currentLang === 'ar' ? 'الفترة المحددة لا تحتوي على أيام إجازة فعلية' : 'The selected period has no actual leave days');
                }

                return fetch('/api/leave/grant', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        financialNo: finNo,
                        leaveTypeId: parseInt(leaveTypeId),
                        fromDate: fromDate,
                        toDate: toDate,
                        reason: reason || null
                    })
                });
            })
            .then(function (r) {
                if (!r.ok) return r.json().then(function (e) { throw new Error(e.error); });
                return r.json();
            })
            .then(function (data) {
                hideLoading();
                lastLeaveTransactions = null;
                var el = $('leaveResultsContent');
                el.innerHTML = '<div class="result-card">' +
                    '<div class="emp-header">' + (currentLang === 'ar' ? 'تم تسجيل الإجازة بنجاح' : 'Leave granted successfully') + '</div>' +
                    '<div class="punch-row"><span class="punch-label">' + (currentLang === 'ar' ? 'من:' : 'From:') + '</span><span class="punch-value">' + fmtDateLocal(data.fromDate) + '</span></div>' +
                    '<div class="punch-row"><span class="punch-label">' + (currentLang === 'ar' ? 'إلى:' : 'To:') + '</span><span class="punch-value">' + fmtDateLocal(data.toDate) + '</span></div>' +
                    '<div class="punch-row"><span class="punch-label">' + (currentLang === 'ar' ? 'عدد الأيام:' : 'Days Count:') + '</span><span class="punch-value">' + data.daysCount + '</span></div>' +
                    '</div>';
                show('leaveResults');
            })
            .catch(function (err) {
                hideLoading();
                showError(err.message);
            });
    });

    function buildLeaveFilterQuery() {
        var params = [];
        var finNo = $('leaveFinNo').value.trim();
        var leaveTypeId = $('leaveTypeId').value;
        var fromDate = $('leaveFrom').value;
        var toDate = $('leaveTo').value;
        if (finNo) params.push('financialNo=' + encodeURIComponent(finNo));
        if (leaveTypeId) params.push('leaveTypeId=' + encodeURIComponent(leaveTypeId));
        if (fromDate) params.push('fromDate=' + fromDate);
        if (toDate) params.push('toDate=' + toDate);
        return params.length ? '?' + params.join('&') : '';
    }

function renderLeaveTransactions(data) {
        var el = $('leaveResultsContent');
        clear(el);

        var consolidated = consolidateLeaveTransactions(data || []);
        var rows = filterAndSortLeaveTransactions(consolidated);
        if (rows.length === 0) {
            el.innerHTML = emptyState(currentLang === 'ar' ? 'لا توجد سجلات إجازات' : 'No leave records found');
            show('leaveResults');
            $('leaveExportActions').style.display = data && data.length ? 'flex' : 'none';
            flashUpdated('leaveResults');
            return;
        }

        var canManage = hasPermission('Leaves');
        var canWorkflow = hasPermission('Leaves') || hasPermission('LeaveHR');
        var canExport = canManage;
        var html = tableScrollOpen(i18n[currentLang].transactions) + '<table><thead><tr><th>#</th><th>' + (currentLang === 'ar' ? 'الرقم المالي' : 'Financial No') + '</th><th>' + (currentLang === 'ar' ? 'الموظف' : 'Employee') + '</th><th>' + (currentLang === 'ar' ? 'النوع' : 'Type') + '</th><th>' + (currentLang === 'ar' ? 'الفترة' : 'Period') + '</th><th>' + (currentLang === 'ar' ? 'أيام العمل' : 'Working Days') + '</th><th>' + (currentLang === 'ar' ? 'السبب' : 'Reason') + '</th><th>' + (currentLang === 'ar' ? 'مدخل الإجازة' : 'Entered By') + '</th><th>' + (currentLang === 'ar' ? 'تاريخ التسجيل' : 'Created') + '</th><th>' + (i18n[currentLang].status || 'Status') + '</th>' + (canManage || canWorkflow ? '<th>' + (i18n[currentLang].actions || 'Actions') + '</th>' : '') + '</tr></thead><tbody>';
        rows.forEach(function (t, i) {
            var reasonText = t.reason || '-';
            if (t.status === 'Rejected' && t.rejectionReason) {
                reasonText += ' <span class="row-sub">— ' + escapeHtml(String(t.rejectionReason)) + '</span>';
            }
            var actions = '';
            if (canManage || canWorkflow) {
                actions = '<div class="row-actions">';
                if (canWorkflow) {
                    actions += '<button class="row-action" type="button" data-leave-action="workflow" data-transaction-id="' + t.transactionId + '">' + (i18n[currentLang].workflow || 'Workflow') + '</button>';
                }
                if (canManage) {
                    actions += '<button class="row-action edit" type="button" data-leave-action="edit" data-transaction-id="' + t.transactionId + '">' + i18n[currentLang].edit + '</button>';
                    actions += '<button class="row-action remove" type="button" data-leave-action="remove" data-transaction-id="' + t.transactionId + '">' + i18n[currentLang].remove + '</button>';
                }
                actions += '</div>';
            }
            html += '<tr>' +
                '<td>' + (i + 1) + '</td>' +
                '<td>' + t.employeeFinancialNo + '</td>' +
                '<td>' + (t.employeeName || '-') + '</td>' +
                '<td>' + (currentLang === 'ar' ? (t.leaveTypeNameAr || t.leaveTypeNameEn || '') : (t.leaveTypeNameEn || t.leaveTypeNameAr || '')) + '</td>' +
                '<td>' + fmtDateLocal(t.fromDate) + ' - ' + fmtDateLocal(t.toDate) + '</td>' +
                '<td>' + t.daysCount + '</td>' +
                '<td>' + reasonText + '</td>' +
                '<td>' + getReadableEnteredBy(t) + '</td>' +
                '<td>' + fmtDateTime(t.createdAt) + '</td>' +
                '<td>' + badge(t.status || 'Approved') + '</td>' +
                (canManage || canWorkflow ? '<td>' + actions + '</td>' : '') +
                '</tr>';
        });
        html += '</tbody></table></div>';
        el.innerHTML = html;
        show('leaveResults');
        $('leaveExportActions').style.display = canExport ? 'flex' : 'none';
        flashUpdated('leaveResults');
    }

    function escapeHtml(value) {
        return String(value).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }

    function consolidateLeaveTransactions(data) {
        var grouped = new Map();
        data.forEach(function (item) {
            var key = item.transactionId != null
                ? 'id:' + item.transactionId
                : [item.employeeFinancialNo, item.leaveTypeId, item.fromDate, item.toDate, item.createdAt].join('|');
            var existing = grouped.get(key);
            if (!existing) {
                existing = Object.assign({}, item, { _rowCount: 0, _daysTotal: 0 });
                grouped.set(key, existing);
            }
            existing._rowCount++;
            existing._daysTotal += Number(item.daysCount) || 0;
        });

        return Array.from(grouped.values()).map(function (item) {
            if (item._rowCount > 1) item.daysCount = item._daysTotal;
            delete item._rowCount;
            delete item._daysTotal;
            return item;
        });
    }

    function getReadableEnteredBy(transaction) {
        var preferred = currentLang === 'ar'
            ? [transaction.enteredByAr, transaction.enteredBy, transaction.enteredByEn]
            : [transaction.enteredByEn, transaction.enteredBy, transaction.enteredByAr];
        for (var i = 0; i < preferred.length; i++) {
            if (isReadableDisplayName(preferred[i])) return preferred[i];
        }
        return '-';
    }

    function filterAndSortLeaveTransactions(data) {
        var term = ($('leaveResultFilter')?.value || '').trim().toLocaleLowerCase();
        var rows = data.filter(function (item) {
            if (!term) return true;
            return [
                item.employeeFinancialNo,
                item.employeeName,
                item.leaveTypeNameAr,
                item.leaveTypeNameEn,
                item.reason,
                item.enteredBy,
                item.enteredByAr,
                item.enteredByEn
            ].some(function (value) {
                return String(value || '').toLocaleLowerCase().indexOf(term) >= 0;
            });
        });

        var sort = $('leaveResultSort')?.value || 'date-desc';
        return rows.slice().sort(function (a, b) {
            if (sort === 'date-asc') return new Date(a.fromDate) - new Date(b.fromDate);
            if (sort === 'employee-asc') return String(a.employeeName || '').localeCompare(String(b.employeeName || ''), currentLang);
            if (sort === 'type-asc') {
                var aType = currentLang === 'ar' ? a.leaveTypeNameAr : a.leaveTypeNameEn;
                var bType = currentLang === 'ar' ? b.leaveTypeNameAr : b.leaveTypeNameEn;
                return String(aType || '').localeCompare(String(bType || ''), currentLang);
            }
            return new Date(b.fromDate) - new Date(a.fromDate);
        });
    }

    $('leaveResultFilter').addEventListener('input', function () {
        if (lastLeaveTransactions) renderLeaveTransactions(lastLeaveTransactions);
    });

    $('exportYear').max = String(new Date().getFullYear());
    $('exportYear').addEventListener('change', renderMonthOptions);
    $('leaveResultSort').addEventListener('change', function () {
        if (lastLeaveTransactions) renderLeaveTransactions(lastLeaveTransactions);
    });

    function findLeaveTransaction(transactionId) {
        return consolidateLeaveTransactions(lastLeaveTransactions || []).find(function (item) {
            return String(item.transactionId) === String(transactionId);
        });
    }

    function openLeaveEditor(transaction) {
        if (!transaction) return;
        loadLeaveTypes().then(function () {
            $('editLeaveId').value = transaction.transactionId;
            $('editLeaveEmployee').textContent = (transaction.employeeName || transaction.employeeFinancialNo || '-');
            $('editLeaveTypeId').value = String(transaction.leaveTypeId);
            $('editLeaveFrom').value = fmtDate(transaction.fromDate);
            $('editLeaveTo').value = fmtDate(transaction.toDate);
            $('editLeaveReason').value = transaction.reason || '';
            $('editLeaveDialog').showModal();
        }).catch(function (error) {
            showError(error.message);
        });
    }

$('leaveResultsContent').addEventListener('click', function (event) {
        var button = event.target.closest('[data-leave-action]');
        if (!button) return;
        var transactionId = button.getAttribute('data-transaction-id');
        var transaction = findLeaveTransaction(transactionId);
        if (button.getAttribute('data-leave-action') === 'workflow') {
            openWorkflowEditor(transaction);
            return;
        }
        if (button.getAttribute('data-leave-action') === 'edit') {
            openLeaveEditor(transaction);
            return;
        }

        var message = currentLang === 'ar'
            ? 'هل تريد حذف سجل الإجازة بالكامل؟'
            : 'Delete this complete leave record?';
        if (!window.confirm(message)) return;
        fetch('/api/leave/transactions/' + encodeURIComponent(transactionId), {
            method: 'DELETE'
        }).then(function (response) {
            if (!response.ok) return response.json().then(function (error) { throw new Error(error.error || 'Delete failed'); });
            return response.json();
        }).then(loadLeaveTransactions)
            .catch(function (error) { showError(error.message); });
    });

$('closeEditLeaveBtn').addEventListener('click', function () { $('editLeaveDialog').close(); });
    $('cancelEditLeaveBtn').addEventListener('click', function () { $('editLeaveDialog').close(); });

    function openWorkflowEditor(transaction) {
        if (!transaction) return;
        $('workflowId').value = transaction.transactionId;
        $('workflowEmployee').textContent = (transaction.employeeName || transaction.employeeFinancialNo || '-');
        $('workflowStatus').value = transaction.status || 'PendingHR';
        $('workflowManager').value = transaction.assignedManager || '';
        $('workflowRejectReason').value = transaction.rejectionReason || '';
        $('workflowDialog').showModal();
    }

    $('closeWorkflowBtn').addEventListener('click', function () { $('workflowDialog').close(); });
    $('cancelWorkflowBtn').addEventListener('click', function () { $('workflowDialog').close(); });
    $('workflowForm').addEventListener('submit', function (event) {
        event.preventDefault();
        var id = $('workflowId').value;
        var status = $('workflowStatus').value;
        var manager = $('workflowManager').value.trim() || null;
        var rejectReason = $('workflowRejectReason').value.trim() || null;
        fetch('/api/leave/transactions/' + encodeURIComponent(id) + '/workflow', {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                status: status,
                managerFinancialNo: manager,
                reason: status === 'Rejected' ? rejectReason : null
            })
        }).then(function (response) {
            if (!response.ok) return response.json().then(function (error) { throw new Error(error.error || 'Workflow update failed'); });
            return response.json();
        }).then(function () {
            $('workflowDialog').close();
            showSuccess(i18n[currentLang].saveChanges);
            return loadLeaveTransactions();
        }).catch(function (error) { showError(error.message); });
    });
    $('editLeaveForm').addEventListener('submit', function (event) {
        event.preventDefault();
        var id = $('editLeaveId').value;
        fetch('/api/leave/transactions/' + encodeURIComponent(id), {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                leaveTypeId: parseInt($('editLeaveTypeId').value, 10),
                fromDate: $('editLeaveFrom').value,
                toDate: $('editLeaveTo').value,
                reason: $('editLeaveReason').value.trim() || null
            })
        }).then(function (response) {
            if (!response.ok) return response.json().then(function (error) { throw new Error(error.error || 'Update failed'); });
            return response.json();
        }).then(function () {
            $('editLeaveDialog').close();
            return loadLeaveTransactions();
        }).catch(function (error) { showError(error.message); });
    });

    function loadLeaveTransactions() {
        showLoading();
        hideError();
        hide('leaveResults');

        var url = '/api/leave/transactions' + buildLeaveFilterQuery();
        return fetch(url)
            .then(readUploadResponse)
            .then(function (data) {
                hideLoading();
                lastLeaveTransactions = data || [];
                renderLeaveTransactions(lastLeaveTransactions);
            })
            .catch(function (err) {
                hideLoading();
                showError(err.message);
            });
    }

$('fetchLeaveTransBtn').addEventListener('click', function () {
        loadLeaveTransactions();
    });

    function loadApprovalQueues() {
        if (!currentUser) return;
        var showManager = hasAnyPermission('LeaveApproval|LeaveHR|Leaves');
        var showHr = hasAnyPermission('LeaveHR|Leaves');
        if (showManager && $('managerApprovalsContent') && $('managerApprovalsCard') && $('managerApprovalsCard').style.display !== 'none') {
            fetch('/api/leave/pending/manager', { cache: 'no-store', credentials: 'same-origin' })
                .then(readUploadResponse)
                .then(function (data) { renderApprovalList($('managerApprovalsContent'), data || [], 'manager'); })
                .catch(function (err) { renderApprovalError($('managerApprovalsContent'), err); });
        }
        if (showHr && $('hrApprovalsContent') && $('hrApprovalsCard') && $('hrApprovalsCard').style.display !== 'none') {
            fetch('/api/leave/pending/hr', { cache: 'no-store', credentials: 'same-origin' })
                .then(readUploadResponse)
                .then(function (data) { renderApprovalList($('hrApprovalsContent'), data || [], 'hr'); })
                .catch(function (err) { renderApprovalError($('hrApprovalsContent'), err); });
        }
    }

    function renderApprovalError(el, err) {
        if (!el) return;
        el.innerHTML = '<div class="empty-state"><strong>' + (currentLang === 'ar' ? 'تعذر تحميل الطلبات' : 'Could not load requests') + '</strong><span>' + escapeHtml(err.message) + '</span></div>';
    }

    function renderApprovalList(el, items, kind) {
        if (!el) return;
        if (!items.length) {
            el.innerHTML = '<div class="empty-state"><strong>' + (i18n[currentLang].noPendingRequests || 'No pending requests') + '</strong></div>';
            return;
        }
        var canReject = kind === 'hr' || hasPermission('LeaveHR') || hasPermission('Leaves');
        var html = items.map(function (t) {
            var type = t.leaveType
                ? (currentLang === 'ar' ? (t.leaveType.nameAr || t.leaveType.nameEn) : (t.leaveType.nameEn || t.leaveType.nameAr))
                : '';
            var empName = t.employee ? t.employee.name : '';
            var actions = '<button type="button" class="success" data-approval-action="approve" data-approval-kind="' + kind + '" data-transaction-id="' + t.id + '">' + i18n[currentLang].approve + '</button>';
            if (canReject) {
                actions += '<button type="button" class="ghost danger-text" data-approval-action="reject" data-approval-kind="' + kind + '" data-transaction-id="' + t.id + '">' + i18n[currentLang].reject + '</button>';
            }
            return '<div class="approval-item">' +
                '<div class="approval-main">' +
                '<strong>' + escapeHtml(empName || '-') + ' <span class="approval-fin">(' + escapeHtml(t.employeeFinancialNo) + ')</span></strong>' +
                '<span class="approval-detail">' + escapeHtml(type || '') + ' • ' + fmtDateLocal(t.fromDate) + ' - ' + fmtDateLocal(t.toDate) + ' • ' + t.daysCount + ' ' + (currentLang === 'ar' ? 'يوم' : 'day(s)') + '</span>' +
                (t.reason ? '<span class="approval-detail">' + escapeHtml(t.reason) + '</span>' : '') +
                '<span class="approval-detail">' + (i18n[currentLang].requested || 'Requested') + ': ' + fmtDateTime(t.createdAt) + '</span>' +
                '</div>' +
                '<div class="approval-actions">' + actions + '</div>' +
                '</div>';
        }).join('');
        el.innerHTML = html;
    }

    document.querySelectorAll('.approval-list').forEach(function (container) {
        container.addEventListener('click', function (event) {
            var button = event.target.closest('[data-approval-action]');
            if (!button) return;
            var id = button.getAttribute('data-transaction-id');
            var action = button.getAttribute('data-approval-action');
            var kind = button.getAttribute('data-approval-kind');

            var pendingReason = action === 'reject'
                ? Promise.resolve(window.prompt(i18n[currentLang].rejectionReasonPrompt, ''))
                : Promise.resolve(null);

            pendingReason.then(function (reason) {
                if (action === 'reject' && reason === null) return Promise.reject(new Error('__cancelled__'));
                var body = action === 'reject' ? { reason: reason || null } : null;
                var endpoint = '/api/leave/' + encodeURIComponent(id) + '/' + (action === 'approve' ? 'approve-' + kind : 'reject');
                return fetch(endpoint, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: body ? JSON.stringify(body) : null
                }).then(function (r) {
                    if (!r.ok) return r.json().then(function (e) { throw new Error(e.error); });
                    return r.json();
                });
            }).then(function () {
                showSuccess(action === 'approve' ? i18n[currentLang].approvedMsg : i18n[currentLang].rejectedMsg);
                loadApprovalQueues();
                if ($('leaveResults') && $('leaveResults').style.display !== 'none') $('fetchLeaveTransBtn').click();
            }).catch(function (err) {
                if (err && err.message === '__cancelled__') return;
                showError(err.message);
            });
        });
    });

    document.querySelectorAll('[data-refresh-approvals]').forEach(function (button) {
        button.addEventListener('click', function () {
            loadApprovalQueues();
            button.classList.add('is-refreshing');
            setTimeout(function () { button.classList.remove('is-refreshing'); }, 600);
        });
    });

    $('exportLeaveExcelBtn').addEventListener('click', function () {
        window.open('/api/leave/export/excel' + buildLeaveFilterQuery(), '_blank');
    });

    $('exportLeavePdfBtn').addEventListener('click', function () {
        var query = buildLeaveFilterQuery();
        window.open('/api/leave/export/pdf' + query + (query ? '&' : '?') + 'lang=' + currentLang, '_blank');
    });

    $('downloadLeaveTemplateBtn').addEventListener('click', function () {
        var language = currentLang === 'ar' ? 'ar' : 'en';
        var fileName = language === 'ar'
            ? 'قالب-استيراد-الإجازات.xlsx'
            : 'leave-upload-template.xlsx';

        var templateUrl = language === 'ar'
            ? '/js/leave-upload-template-ar.js'
            : '/js/leave-upload-template-en.js';

        downloadBlobFile(templateUrl, fileName);
    });

    $('downloadEmployeesTemplateBtn').addEventListener('click', function () {
        downloadBlobFile('/api/import/template/employees?lang=' + encodeURIComponent(currentLang), 'employees-template.xlsx');
    });

    $('downloadBalancesTemplateBtn').addEventListener('click', function () {
        downloadBlobFile('/api/import/template/balances?lang=' + encodeURIComponent(currentLang), 'balances-template.xlsx');
    });

    // Leaves Upload
    $('uploadLeavesBtn').addEventListener('click', function () {
        performUpload('leavesUploadFile', function () {
            lastLeaveTransactions = null;
            hide('leaveResults');
            $('fetchLeaveTransBtn').click();
        });
    });

    function getExportParams() {
        var year = $('exportYear').value;
        var month = $('exportMonth').value;
        var dept = $('exportDept').value;
        var level = $('exportLevel').value;
        var area = $('exportArea').value;
        if (!year || !month) { showError(currentLang === 'ar' ? 'يرجى اختيار السنة والشهر' : 'Please select year and month'); return null; }
        var params = 'year=' + year + '&month=' + month;
        if (dept) params += '&department=' + encodeURIComponent(dept);
        if (level) params += '&level=' + encodeURIComponent(level);
        if (area) params += '&area=' + encodeURIComponent(area);
        updateExportActiveFilters();
        return { params: params, year: year, month: month, dept: dept, level: level, area: area };
    }

    function renderMonthlyPreview(data, info) {
        var el = $('exportPreviewContent');
        clear(el);

        if (!data || data.length === 0 || !info) {
            el.innerHTML = emptyState(currentLang === 'ar' ? 'لا توجد بيانات لهذا الشهر' : 'No data for this month');
            hide('monthlyExportActions');
            show('exportPreview');
            flashUpdated('exportPreview');
            return;
        }

        var daysInMonth = new Date(parseInt(info.year), parseInt(info.month), 0).getDate();
        var displayDays = 31;
        var monthLabel = (monthNames[currentLang] || monthNames.ar)[parseInt(info.month) - 1];
        var labels = currentLang === 'ar'
            ? {
                caption: 'فترة كشف الحضور ' + monthLabel + ' ' + info.year,
                pr: 'الرقم المالي', total: 'إجمالي أيام العمل', signature: 'توقيع الموظف', present: 'أيام<br>حضور', regular: 'إجازة<br>اعتيادي', sick: 'أيام<br>مرضي', casual: 'أيام<br>عارضة', absent: 'غياب', rest: 'راحة', extMission: 'مأمورية<br>خارجية', intMission: 'مأمورية<br>داخلية', training: 'دورة<br>تدريب', department: 'الإدارة العامة', level: 'المستوى الوظيفي', name: 'الاسم', jobTitle: 'الوظيفة', box: 'الصندوق'
            }
            : {
                caption: 'Time sheet period ' + monthLabel + ' ' + info.year,
                pr: 'Financial No', total: 'Total Working Days', signature: 'Employee Signature', present: 'Present<br>Days', regular: 'Regular<br>Leave', sick: 'Sick<br>Days', casual: 'Casual<br>Leave', absent: 'Absent', rest: 'Rest', extMission: 'External<br>Mission', intMission: 'Internal<br>Mission', training: 'Training', department: 'Department', level: 'Level', name: 'Name', jobTitle: 'Job Title', box: 'Box'
            };

        var sheetDirection = currentLang === 'ar' ? 'rtl' : 'ltr';
        var html = '<div class="monthly-attendance-wrap" dir="' + sheetDirection + '"><table class="monthly-attendance-table template-preview" dir="' + sheetDirection + '">';
        html += '<colgroup><col class="col-pr"><col class="col-name"><col class="col-job">';
        for (var columnDay = 1; columnDay <= 31; columnDay++) html += '<col class="col-day">';
        html += '<col class="col-total"><col class="col-signature">';
        for (var columnSummary = 0; columnSummary < 9; columnSummary++) html += '<col class="col-summary">';
        html += '<col class="col-department"><col class="col-level"><col class="col-box"></colgroup>';
        html += '<thead>';
        html += '<tr class="template-title"><th></th><th colspan="44">' + labels.caption + '</th><th colspan="3"></th></tr>';
        html += '<tr class="template-meta">';
        html += '<th colspan="2"></th>';
        html += '<th colspan="8">' + (info.dept || (currentLang === 'ar' ? 'جميع الإدارات' : 'All Departments')) + '</th>';
        html += '<th colspan="2">Dep :</th>';
        html += '<th colspan="7">' + (currentLang === 'ar' ? 'Loction : المركز الرئيسى' : 'Location : Main Center') + '</th>';
        html += '<th colspan="29"></th>';
        html += '</tr>';
        var legendRows = [
            [['A', 'Annual', 1, 1], ['C', 'Casual', 2, 2], ['P', 'Permission', 3, 3], ['E', 'Earaned', 4, 5], ['R', 'Weekend', 6, 13], ['H', 'Holiday', 14, 20], ['ML', 'Military', 21, 27], ['J', 'Hajj', 28, 35]],
            [['X', 'Work', 1, 1], ['B', 'Absent', 2, 2], ['DX', 'Absent', 3, 3], ['DI', 'Duty I', 4, 5], ['S', 'Sick Leave', 6, 13], ['CS', 'Chronic Sick', 14, 20], ['I', 'Infection', 21, 27], ['N', 'Accident', 28, 34], ['T', 'Training', 35, 36]],
            [['O', 'Others', 1, 1], ['MA', 'Maternity', 2, 2], ['K', 'Maternity', 3, 3], ['HT', 'Half Time', 4, 5], ['VW', 'Vacation Without Pay', 6, 13], ['RD', 'labor Reduction', 14, 20], ['WH', 'Work From Home', 21, 27], ['EV', 'Extraordinary vacation', 28, 35]]
        ];
        legendRows.forEach(function (items) {
            html += '<tr class="template-legend">';
            html += '<th colspan="36"><div class="template-legend-grid">';
            items.forEach(function (item) {
                html += '<span class="template-legend-item" style="grid-column:' + item[2] + ' / ' + (item[3] + 1) + '">';
                html += '<b>' + item[0] + '</b><span>' + item[1] + '</span></span>';
            });
            html += '</div></th><th colspan="12" class="template-legend-space"></th>';
            html += '</tr>';
        });
        html += '<tr>';
        html += '<th colspan="36" class="template-header-blank"></th>';
        html += '<th class="srow-ar">' + labels.present + '</th>';
        html += '<th class="srow-ar">' + labels.regular + '</th>';
        html += '<th class="srow-ar">' + labels.sick + '</th>';
        html += '<th class="srow-ar">' + labels.casual + '</th>';
        html += '<th class="srow-ar">' + labels.absent + '</th>';
        html += '<th class="srow-ar">' + labels.rest + '</th>';
        html += '<th class="srow-ar">' + labels.extMission + '</th>';
        html += '<th class="srow-ar">' + labels.intMission + '</th>';
        html += '<th class="srow-ar">' + labels.training + '</th>';
        html += '<th colspan="3" class="template-header-blank"></th>';
        html += '</tr><tr>';
        html += '<th>' + labels.pr + '</th><th>' + labels.name + '</th><th>' + labels.jobTitle + '</th>';
        for (var d = 1; d <= displayDays; d++) {
            var dow = new Date(parseInt(info.year), parseInt(info.month) - 1, d).getDay();
            var cls = (d > daysInMonth || dow === 5 || dow === 6) ? ' class="sday-gray"' : '';
            html += '<th' + cls + '>' + d + '</th>';
        }
        html += '<th>' + labels.total + '</th><th>' + labels.signature + '</th>';
        html += '<th>X</th><th>A</th><th>S</th><th>C</th><th>B</th><th>E</th><th>DI</th><th>DX</th><th>T</th>';
        html += '<th>' + labels.department + '</th><th>' + labels.level + '</th><th>' + labels.box + '</th>';
        html += '</tr></thead><tbody>';

        data.forEach(function (emp) {
            html += '<tr><td>' + emp.financialNo + '</td><td class="name-cell">' + (emp.name || '') + '</td><td class="job-cell">' + (emp.jobTitle || '') + '</td>';
            var presentDays = 0, regLeave = 0, sickDays = 0, casualDays = 0, absenceDays = 0, restDays = 0, extMission = 0, intMission = 0, trainingDays = 0;

            for (var d = 1; d <= displayDays; d++) {
                var code = d <= daysInMonth && emp.dailyCodes && emp.dailyCodes[d - 1] ? emp.dailyCodes[d - 1] : '';
                var dow = new Date(parseInt(info.year), parseInt(info.month) - 1, d).getDay();
                var cellCls = 'code-cell';
                if (d > daysInMonth || dow === 5 || dow === 6) cellCls += ' sday-gray';
                html += '<td class="' + cellCls + '">' + code + '</td>';

                if (code) {
                    var cu = code.toUpperCase();
                    if (cu === 'WH' || cu === 'X' || cu === 'X1' || cu === 'X2' || cu === 'X3' || cu === 'X4') presentDays++;
                    else if (cu === 'A') regLeave++;
                    else if (cu === 'S') sickDays++;
                    else if (cu === 'C') casualDays++;
                    else if (cu === 'B') absenceDays++;
                    else if (cu === 'E') restDays++;
                    else if (cu === 'DI') extMission++;
                    else if (cu === 'DX') intMission++;
                    else if (cu === 'T') trainingDays++;
                }
            }

            html += '<td class="total-cell">' + presentDays + '</td><td></td>';
            html += '<td class="count-cell">' + presentDays + '</td><td class="count-cell">' + regLeave + '</td><td class="count-cell">' + sickDays + '</td><td class="count-cell">' + casualDays + '</td><td class="count-cell">' + absenceDays + '</td><td class="count-cell">' + restDays + '</td><td class="count-cell">' + extMission + '</td><td class="count-cell">' + intMission + '</td><td class="count-cell">' + trainingDays + '</td>';
            html += '<td class="dept-cell">' + (emp.department || '') + '</td><td>' + (emp.level || '') + '</td><td></td></tr>';
        });

        html += '</tbody></table></div>';
        el.innerHTML = html;
        var exportActions = $('monthlyExportActions');
        if (exportActions) exportActions.style.display = '';
        show('exportPreview');
        flashUpdated('exportPreview');
    }

    $('exportPreviewBtn').addEventListener('click', function () {
        var info = getExportParams();
        if (!info) return;

        showLoading();
        hideError();
        hide('exportPreview');
        hide('exportResults');

        fetch('/api/export/preview?' + info.params)
            .then(function (r) { if (!r.ok) throw new Error(currentLang === 'ar' ? 'فشل التحميل' : 'Failed to load'); return r.json(); })
            .then(function (data) {
                hideLoading();
                lastMonthlyPreview = data || [];
                lastMonthlyInfo = info;
                renderMonthlyPreview(lastMonthlyPreview, lastMonthlyInfo);
            })
            .catch(function (err) {
                hideLoading();
                showError(err.message);
            });
    });

    $('monthlyExportExcelBtn').addEventListener('click', function () {
        downloadMonthly('excel');
    });

    $('monthlyExportPdfBtn').addEventListener('click', function () {
        downloadMonthly('pdf');
    });

    var monthlyExcelTopBtn = $('downloadMonthlyExcelTopBtn');
    if (monthlyExcelTopBtn) monthlyExcelTopBtn.addEventListener('click', function () { downloadMonthly('excel'); });

    var monthlyPdfTopBtn = $('downloadMonthlyPdfTopBtn');
    if (monthlyPdfTopBtn) monthlyPdfTopBtn.addEventListener('click', function () { downloadMonthly('pdf'); });

    function downloadMonthly(type) {
        var info = getExportParams();
        if (!info) return;
        var path = type === 'pdf' ? '/api/export/monthly/pdf?' : '/api/export/monthly?';
        window.open(path + info.params + (type === 'pdf' ? '&lang=' + currentLang : ''), '_blank');
    }

    $('fetchDailyBtn').addEventListener('click', function () {
        var date = $('dailyDate').value;
        if (!date) {
            showError(currentLang === 'ar' ? 'يرجى اختيار تاريخ التقرير' : 'Select the report date');
            return;
        }

        showLoading();
        hideError();
        hide('dailyResults');
        hide('dailyExportActions');
        fetch('/api/tracking/top-management/daily?date=' + encodeURIComponent(date) + '&_=' + Date.now(), {
            cache: 'no-store',
            credentials: 'same-origin'
        }).then(readUploadResponse)
            .then(function (data) {
                hideLoading();
                lastDailyRows = data || [];
                renderDaily(lastDailyRows);
            })
            .catch(function (error) {
                hideLoading();
                showError(error.message);
            });
    });

    function openDailyExport(format) {
        var date = $('dailyDate').value;
        if (!date) {
            showError(currentLang === 'ar' ? 'يرجى اختيار تاريخ التقرير' : 'Select the report date');
            return;
        }
        var params = new URLSearchParams({ date: date, lang: currentLang, _: Date.now() });
        window.open('/api/tracking/top-management/daily/export/' + format + '?' + params.toString(), '_blank');
    }

    $('exportDailyExcelBtn').addEventListener('click', function () { openDailyExport('excel'); });
    $('exportDailyPdfBtn').addEventListener('click', function () { openDailyExport('pdf'); });

    function getPermissionSelection() {
        return Array.from(document.querySelectorAll('#adminPermissions input[type="checkbox"]:checked')).map(function (x) { return x.value; });
    }

    function renderPermissions(permissions, selected) {
        var el = $('adminPermissions');
        if (!el) return;
        var selectedSet = new Set(selected || permissions);
        el.innerHTML = permissions.map(function (p) {
            var checked = selectedSet.has(p) ? ' checked' : '';
            return '<label class="permission-chip"><input type="checkbox" value="' + p + '"' + checked + '> ' + permissionLabel(p) + '</label>';
        }).join('');
    }

    function loadAdminData() {
        if (!$('adminResultsContent')) return;
        Promise.all([
            fetch('/api/admin/permissions').then(function (r) { return r.json(); }),
            fetch('/api/admin/users').then(function (r) { return r.json(); }),
            fetch('/api/admin/day-settings').then(function (r) { return r.json(); })
        ]).then(function (items) {
            var perms = items[0];
            var users = items[1] || [];
            var days = items[2] || [];
            var role = $('adminRole').value;
            lastPermissionsAll = perms.permissions || [];
            lastAdminUsers = users;
            lastAdminDays = days;
populateResetPasswordUsers(users);
            renderPermissions(lastPermissionsAll, role === 'Admin' ? perms.admin : perms.employee);
            renderAdminTables(users, days);
            loadAdSyncStatus();
        }).catch(function () {});
    }

    function loadAdSyncStatus() {
        var card = $('adSyncStatus');
        if (!card) return;
        fetch('/api/admin/ad-sync-status', { cache: 'no-store' })
            .then(function (r) {
                if (!r.ok) throw new Error('status');
                return r.json();
            })
            .then(function (data) {
                var text;
                if (!data || !data.lastRunAt) {
                    text = (i18n[currentLang].adSyncNeverRun || 'Sync has not run yet');
                } else {
                    var result = data.successCount + ' / ' + data.failedCount;
                    text = (i18n[currentLang].adSyncLastRun || 'Last sync:') + ' ' + fmtDateTime(data.lastRunAt) + ' — ' + result;
                }
                card.textContent = text;
                card.classList.remove('ad-error');
            })
            .catch(function () {
                card.textContent = (i18n[currentLang].adSyncNeverRun || 'Sync has not run yet');
            });
    }

    if ($('runAdSyncBtn')) {
        $('runAdSyncBtn').addEventListener('click', function () {
            var button = $('runAdSyncBtn');
            button.disabled = true;
            button.textContent = i18n[currentLang].adSyncRunning;
            fetch('/api/admin/ad-sync', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: '{}'
            }).then(function (r) {
                if (!r.ok) return r.json().then(function (e) { throw new Error(e.error || 'Sync failed'); });
                return r.json();
            }).then(function (data) {
                var created = data.created || 0;
                var updated = data.updated || 0;
                var failed = data.failed || 0;
                var skipped = data.skipped || 0;
                showSuccess('AD: ' + created + ' ' + (currentLang === 'ar' ? 'جديد' : 'created') + ' / ' + updated + ' ' + (currentLang === 'ar' ? 'محدث' : 'updated') + ' / ' + failed + ' ' + (currentLang === 'ar' ? 'فشل' : 'failed') + ' / ' + skipped + ' ' + (currentLang === 'ar' ? 'تخطي' : 'skipped'));
                loadAdSyncStatus();
            }).catch(function (err) {
                showError(err.message);
            }).finally(function () {
                button.disabled = false;
                button.textContent = i18n[currentLang].runAdSync;
            });
        });
    }

    if ($('saveManagerBtn')) {
        $('saveManagerBtn').addEventListener('click', function () {
            var employeeNo = $('managerEmpNo').value.trim();
            var managerNo = $('managerManagerNo').value.trim() || null;
            if (!employeeNo) { showError(currentLang === 'ar' ? 'الرقم المالي للموظف مطلوب' : 'Employee financial number is required'); return; }
            hideError();
            fetch('/api/admin/employees/' + encodeURIComponent(employeeNo) + '/manager', {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ managerFinancialNo: managerNo })
            }).then(function (r) {
                if (!r.ok) return r.json().then(function (e) { throw new Error(e.error); });
                return r.json();
            }).then(function () {
                showSuccess(i18n[currentLang].managerSaved);
                $('managerEmpNo').value = '';
                $('managerManagerNo').value = '';
            }).catch(function (err) {
                showError(err.message);
            });
        });
    }

    function populateResetPasswordUsers(users) {
        var select = $('resetPasswordUser');
        if (!select) return;
        var selectedId = select.value;
        select.replaceChildren();
        (users || []).forEach(function (user) {
            var option = document.createElement('option');
            option.value = String(user.id);
            option.textContent = getUserDisplayName(user) + ' (' + user.username + ')';
            select.appendChild(option);
        });
        if (selectedId) select.value = selectedId;
    }

    function renderAdminTables(users, days) {
        var el = $('adminResultsContent');
        if (!el) return;
        var html = '<div class="two-column admin-tables">';
        html += '<div><h4>' + (currentLang === 'ar' ? 'المستخدمون' : 'Users') + '</h4><table><thead><tr><th>' + (currentLang === 'ar' ? 'المعرف' : 'ID') + '</th><th>' + (currentLang === 'ar' ? 'المستخدم' : 'User') + '</th><th>' + (currentLang === 'ar' ? 'الاسم' : 'Name') + '</th><th>' + (currentLang === 'ar' ? 'الدور' : 'Role') + '</th><th>' + (currentLang === 'ar' ? 'الصلاحيات' : 'Permissions') + '</th></tr></thead><tbody>';
        users.forEach(function (u) {
            html += '<tr><td>' + u.id + '</td><td>' + u.username + '</td><td>' + getUserDisplayName(u) + '</td><td>' + roleLabel(u.role, u.role === 'Admin') + '</td><td>' + permissionListLabel(u.permissions) + '</td></tr>';
        });
        html += '</tbody></table></div>';
        html += '<div><h4>' + (currentLang === 'ar' ? 'إعدادات الأيام' : 'Day Settings') + '</h4><table><thead><tr><th>' + (currentLang === 'ar' ? 'التاريخ' : 'Date') + '</th><th>' + (currentLang === 'ar' ? 'النوع' : 'Type') + '</th><th>' + (currentLang === 'ar' ? 'ملاحظات' : 'Notes') + '</th></tr></thead><tbody>';
        days.forEach(function (d) {
            html += '<tr><td>' + fmtDateLocal(d.date) + '</td><td>' + statusLabel(d.dayType) + '</td><td>' + (d.notes || '') + '</td></tr>';
        });
        html += '</tbody></table></div></div>';
        el.innerHTML = html;
        show('adminResults');
        flashUpdated('adminResults');
    }

    if ($('adminRole')) {
        $('adminRole').addEventListener('change', loadAdminData);
$('saveUserBtn').addEventListener('click', function () {
            var username = $('adminUsername').value.trim();
            if (!username) { showError(currentLang === 'ar' ? 'اسم المستخدم مطلوب' : 'Username is required'); return; }
            hideError();
            fetch('/api/admin/users', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ username: username, password: $('adminPassword').value, displayName: $('adminDisplayNameEn').value.trim() || $('adminDisplayNameAr').value.trim(), displayNameAr: $('adminDisplayNameAr').value.trim(), displayNameEn: $('adminDisplayNameEn').value.trim(), role: $('adminRole').value, permissions: getPermissionSelection(), isActive: true })
            }).then(function (r) {
                if (!r.ok) {
                    return r.json().then(function (e) {
                        throw new Error(e && e.error ? e.error : (currentLang === 'ar' ? 'تعذر حفظ المستخدم' : 'Could not save user'));
                    });
                }
                return r.json();
            })
                .then(loadAdminData)
                .catch(function (err) { showError(err.message); });
        });

    $('resetPasswordBtn').addEventListener('click', function () {
            var userId = $('resetPasswordUser').value;
            var password = $('resetPasswordValue').value;
            var confirmation = $('resetPasswordConfirm').value;
            if (!userId) {
                showError(currentLang === 'ar' ? 'يرجى اختيار المستخدم' : 'Select a user');
                return;
            }
            if (password.length < 10) {
                showError(currentLang === 'ar' ? 'يجب ألا تقل كلمة المرور عن 10 أحرف' : 'Password must contain at least 10 characters');
                return;
            }
            if (password !== confirmation) {
                showError(currentLang === 'ar' ? 'كلمتا المرور غير متطابقتين' : 'Passwords do not match');
                return;
            }

            hideError();
            fetch('/api/admin/users/' + encodeURIComponent(userId) + '/reset-password', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ password: password })
}).then(function (r) {
                if (!r.ok) {
                    return r.json().then(function (e) {
                        throw new Error(e && e.error ? e.error : (currentLang === 'ar' ? 'تعذر إعادة تعيين كلمة المرور' : 'Could not reset password'));
                    });
                }
                return r.json();
            }).then(function () {
                $('resetPasswordValue').value = '';
                $('resetPasswordConfirm').value = '';
                loadAdminData();
            }).catch(function (err) { showError(err.message); });
        });

        $('saveCalendarDayBtn').addEventListener('click', function () {
            var date = $('calendarDate').value;
            if (!date) { showError(currentLang === 'ar' ? 'يرجى اختيار التاريخ' : 'Please select a date'); return; }
            hideError();
            fetch('/api/admin/day-settings', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ date: date, dayType: $('calendarDayType').value, notes: $('calendarNotes').value.trim() })
            }).then(function (r) { if (!r.ok) throw new Error(currentLang === 'ar' ? 'تعذر حفظ اليوم' : 'Could not save day'); return r.json(); })
                .then(loadAdminData)
                .catch(function (err) { showError(err.message); });
        });

        loadAdminData();
    }

    function triggerRefresh(section, button) {
        if (button) button.classList.add('is-refreshing');
        hideError();

        switch (section) {
            case 'attendance':
                $('fetchAttendanceBtn').click();
                break;
            case 'employees':
                $('searchEmpBtn').click();
                break;
            case 'balances':
                $('fetchBalBtn').click();
                break;
            case 'leave':
                if ($('leaveResults') && $('leaveResults').style.display !== 'none') $('fetchLeaveTransBtn').click();
                else updateLeaveDays();
                break;
            case 'monthly':
                $('exportPreviewBtn').click();
                break;
            case 'daily':
                $('fetchDailyBtn').click();
                break;
            case 'wage':
                fetchWageReport();
                break;
            case 'reports':
                if ($('reportsResults') && $('reportsResults').style.display !== 'none') fetchOvertimeReport();
                if ($('dailyResults') && $('dailyResults').style.display !== 'none') $('fetchDailyBtn').click();
                if ($('wageResults') && $('wageResults').style.display !== 'none') fetchWageReport();
                break;
            case 'admin':
                loadAdminData();
                break;
        }

        updateSyncStatus();
        setTimeout(function () { if (button) button.classList.remove('is-refreshing'); }, 900);
    }

    document.querySelectorAll('[data-refresh]').forEach(function (button) {
        button.addEventListener('click', function () {
            triggerRefresh(button.getAttribute('data-refresh'), button);
        });
    });

    function saveEmployeeSchedules(clearOverride) {
        var numbers = $('scheduleFinancialNumbers').value
            .split(',')
            .map(function (value) { return value.trim(); })
            .filter(Boolean);
        if (!numbers.length) {
            showError(currentLang === 'ar' ? 'يرجى إدخال رقم مالي واحد على الأقل' : 'Enter at least one financial number');
            return;
        }

        var startTime = clearOverride ? null : $('scheduleStart').value;
        var endTime = clearOverride ? null : $('scheduleEnd').value;
        if (!clearOverride && (!startTime || !endTime)) {
            showError(currentLang === 'ar' ? 'يرجى تحديد بداية ونهاية العمل' : 'Select both start and end times');
            return;
        }

        hideError();
        showLoading();
        fetch('/api/admin/employee-schedules', {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                financialNumbers: numbers,
                startTime: startTime,
                endTime: endTime
            })
        }).then(function (response) {
            if (!response.ok) return response.json().then(function (error) { throw new Error(error.error || 'Schedule update failed'); });
            return response.json();
        }).then(function (result) {
            hideLoading();
            var message = currentLang === 'ar'
                ? 'تم تحديث مواعيد ' + result.updatedCount + ' موظف'
                : result.updatedCount + ' employee schedule(s) updated';
            if (result.missingFinancialNumbers && result.missingFinancialNumbers.length) {
                message += (currentLang === 'ar' ? ' - أرقام غير موجودة: ' : ' - Not found: ')
                    + result.missingFinancialNumbers.join(', ');
            }
            $('scheduleUpdateResult').textContent = message;
        }).catch(function (error) {
            hideLoading();
            showError(error.message);
        });
    }

    $('saveEmployeeScheduleBtn').addEventListener('click', function () {
        saveEmployeeSchedules(false);
    });
    $('clearEmployeeScheduleBtn').addEventListener('click', function () {
        saveEmployeeSchedules(true);
    });

    function updateSyncStatus() {
        if (!currentUser) return;
        fetch('/api/import/sync-status')
            .then(readUploadResponse)
            .then(function (data) {
                var el = $('syncStatus');
                if (!el) return;
                if (!data.lastSyncTime) {
                    el.innerHTML = '<span style="color:#c8882a;">&#9679;</span> ' + ((i18n[currentLang] || i18n.ar).dataFreshPending);
                    return;
                }
                var time = new Date(data.lastSyncTime);
                var h = String(time.getHours()).padStart(2, '0');
                var m = String(time.getMinutes()).padStart(2, '0');
                var s = String(time.getSeconds()).padStart(2, '0');
                var color = data.isRunning ? '#188038' : '#666';
                var label = (i18n[currentLang] || i18n.ar).lastUpdate;
                el.innerHTML = '<span style="color:' + color + ';">&#9679;</span> ' + label + ' ' + h + ':' + m + ':' + s;
            })
            .catch(function () {
                var el = $('syncStatus');
                if (!el) return;
                var msg = (i18n[currentLang] || i18n.ar).dataFreshFailed;
                el.innerHTML = '<span style="color:#dc3545;">&#9679;</span> ' + msg;
            });
    }

    loadCurrentUser();
    setInterval(updateSyncStatus, 10000);

    applyLanguage();
})();
