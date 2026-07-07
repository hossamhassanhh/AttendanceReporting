(function () {
    var currentLang = 'ar';

    var i18n = {
        ar: {
            appTitle: 'منصة الحضور والإجازات',
            general: 'عام',
            navAttendance: 'الحضور',
            navEmployees: 'الموظفين',
            navBalances: 'الأرصدة',
            navLeave: 'الإجازات',
            navMonthly: 'التقرير الشهري',
            userName: 'حسام حسن',
            departmentName: 'تكنولوجيا المعلومات',
            langAr: 'عربي',
            langEn: 'إنجليزي',
            roleEmployee: 'موظف إداري',
            hrPortal: 'بوابة الموارد البشرية',
            attendanceKicker: 'الحضور',
            attendanceTitle: 'تقرير الحضور',
            attendanceDesc: 'استعرض سجلات الحضور والانصراف حسب الفترة والموظف أو كل الموظفين.',
            employeeNumbers: 'أرقام الموظفين (مفصولة بفاصلة)',
            employeeNumbersPlaceholder: 'مثال: 4779, 4780 - اتركه فارغاً للكل',
            fromDate: 'من تاريخ',
            toDate: 'إلى تاريخ',
            showAttendance: 'عرض الحضور',
            attendanceResults: 'نتائج الحضور',
            exportExcel: 'تصدير إكسيل',
            exportPdf: 'تصدير PDF',
            employeesKicker: 'الموظفون',
            employeesTitle: 'بيانات الموظفين',
            employeesDesc: 'ابحث في قاعدة بيانات الموظفين أو اعرض كل الموظفين المسجلين.',
            searchNameOrNumber: 'بحث (اسم / رقم مالي)',
            allEmployeesPlaceholder: 'اتركه فارغاً لعرض كل الموظفين',
            search: 'بحث',
            uploadEmployeesTitle: 'رفع بيانات الموظفين',
            uploadEmployeesDesc: 'تحديث الأسماء، الوظائف، الإدارات، والمستويات.',
            uploadData: 'رفع البيانات',
            searchResults: 'نتائج البحث',
            balancesKicker: 'الأرصدة',
            balancesTitle: 'أرصدة الإجازات',
            balancesDesc: 'راجع حصص الإجازات السنوية لكل الموظفين أو لموظف محدد.',
            financialNo: 'رقم مالي',
            allBalancesPlaceholder: 'اتركه فارغاً لعرض كل الأرصدة',
            showBalance: 'عرض الرصيد',
            uploadBalancesTitle: 'رفع أرصدة الإجازات',
            uploadBalancesDesc: 'تحديث حصص الإجازات لكل الموظفين من ملف إكسيل.',
            uploadBalances: 'رفع الأرصدة',
            balance: 'الرصيد',
            leaveKicker: 'إدارة الإجازات',
            leaveTitle: 'إدارة الإجازات',
            leaveDesc: 'سجل الإجازات، راجع المعاملات، وارفع قوالب الإجازات الجماعية.',
            leaveFinancialPlaceholder: 'رقم مالي للإضافة، أو اتركه فارغاً لعرض كل السجلات',
            leaveType: 'نوع الإجازة',
            daysCount: 'عدد الأيام',
            reason: 'السبب',
            reasonPlaceholder: 'سبب الإجازة',
            grantLeave: 'منح الإجازة',
            showLeaveLog: 'عرض سجل الإجازات',
            downloadLeaveTemplate: 'تحميل قالب الإجازات',
            downloadTemplate: 'تحميل القالب',
            uploadLeavesTitle: 'رفع أيام الإجازات',
            uploadLeavesDesc: 'إضافة معاملات إجازة متعددة دفعة واحدة.',
            uploadLeaves: 'رفع الإجازات',
            transactions: 'المعاملات',
            monthlyKicker: 'التقرير الشهري',
            monthlyTitle: 'جدول الحضور الشهري',
            monthlyDesc: 'إنشاء تقرير الحضور الشهري من بيانات الحضور والإجازات حسب الشهر والإدارة.',
            year: 'السنة',
            month: 'الشهر',
            department: 'الإدارة',
            all: 'الكل',
            showReport: 'عرض التقرير',
            uploadMonthlyTitle: 'رفع بيانات التقرير الشهري',
            uploadMonthlyDesc: 'دمج أكواد الحضور المستوردة مع بيانات الحضور والإجازات.',
            uploadReportData: 'رفع بيانات التقرير',
            result: 'النتائج',
            loading: 'جاري التحميل...',
            footerText: 'تطبيق ويب للموارد البشرية والتقارير التشغيلية'
        },
        en: {
            appTitle: 'Attendance and Leave Platform',
            general: 'General',
            navAttendance: 'Attendance',
            navEmployees: 'Employees',
            navBalances: 'Balances',
            navLeave: 'Leaves',
            navMonthly: 'Monthly Report',
            userName: 'Hossam Hassan',
            departmentName: 'Information Technology',
            langAr: 'Arabic',
            langEn: 'English',
            roleEmployee: 'Administrative Employee',
            hrPortal: 'HR Portal',
            attendanceKicker: 'Attendance',
            attendanceTitle: 'Attendance Report',
            attendanceDesc: 'Review attendance and departure records by date range and employee, or all employees.',
            employeeNumbers: 'Employee numbers (comma separated)',
            employeeNumbersPlaceholder: 'Example: 4779, 4780 - leave blank for all',
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
            allEmployeesPlaceholder: 'Leave blank to show all employees',
            search: 'Search',
            uploadEmployeesTitle: 'Upload Employee Data',
            uploadEmployeesDesc: 'Update names, titles, departments, and levels.',
            uploadData: 'Upload Data',
            searchResults: 'Search Results',
            balancesKicker: 'Balances',
            balancesTitle: 'Leave Balances',
            balancesDesc: 'Review annual leave quotas for all employees or a specific employee.',
            financialNo: 'Financial number',
            allBalancesPlaceholder: 'Leave blank to show all balances',
            showBalance: 'Show Balance',
            uploadBalancesTitle: 'Upload Leave Balances',
            uploadBalancesDesc: 'Update leave quotas for all employees from an Excel file.',
            uploadBalances: 'Upload Balances',
            balance: 'Balance',
            leaveKicker: 'Leave Management',
            leaveTitle: 'Leave Management',
            leaveDesc: 'Grant leaves, review transactions, and upload bulk leave templates.',
            leaveFinancialPlaceholder: 'Financial number to grant, or leave blank to show all records',
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
            uploadMonthlyTitle: 'Upload Monthly Report Data',
            uploadMonthlyDesc: 'Merge imported monthly codes with attendance and leave data.',
            uploadReportData: 'Upload Report Data',
            result: 'Result',
            loading: 'Loading...',
            footerText: 'HR and operational reporting web application'
        }
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

        document.querySelectorAll('.lang-pill').forEach(function (pill) {
            pill.classList.toggle('active-lang', pill.getAttribute('data-lang') === currentLang);
        });

        var todayEl = $('todayLabel');
        if (todayEl) {
            var now = new Date();
            var day = now.getDate();
            if (currentLang === 'ar') {
                todayEl.textContent = day + ' ' + arMonths[now.getMonth()] + ' ' + now.getFullYear();
            } else {
                var enMonths = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
                todayEl.textContent = enMonths[now.getMonth()] + ' ' + day + ', ' + now.getFullYear();
            }
        }

        if (currentLang === 'ar') {
            document.documentElement.setAttribute('lang', 'ar');
            document.documentElement.setAttribute('dir', 'rtl');
        } else {
            document.documentElement.setAttribute('lang', 'en');
            document.documentElement.setAttribute('dir', 'ltr');
        }
    }

    document.querySelectorAll('.lang-pill').forEach(function (pill) {
        pill.addEventListener('click', function () {
            currentLang = this.getAttribute('data-lang') || 'ar';
            applyLanguage();
        });
    });

    function $(id) { return document.getElementById(id); }
    function show(id) { var el = $(id); if (el) el.style.display = 'block'; }
    function hide(id) { var el = $(id); if (el) el.style.display = 'none'; }

    function showError(msg) {
        $('errorMessage').textContent = msg;
        show('error');
    }

    function hideError() { hide('error'); }

    function showLoading() {
        show('loading');
        hide('error');
    }

    function hideLoading() { hide('loading'); }

    function fmtDate(d) {
        if (!d) return '-';
        if (typeof d === 'string') d = d.split('T')[0];
        return d;
    }

    var arMonths = ['يناير', 'فبراير', 'مارس', 'ابريل', 'مايو', 'يونيو', 'يوليو', 'اغسطس', 'سبتمبر', 'اكتوبر', 'نوفمبر', 'ديسمبر'];
    var arDays = ['الأحد', 'الإثنين', 'الثلاثاء', 'الأربعاء', 'الخميس', 'الجمعة', 'السبت'];

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

    function fmtScheduleAr(time) {
        if (!time) return '-';
        var parts = time.split(':');
        var h = parseInt(parts[0], 10);
        var m = parts[1] || '00';
        var ampm = h >= 12 ? 'م' : 'ص';
        h = h % 12 || 12;
        return h + ':' + m + ' ' + ampm;
    }

    var statusArMap = {
        'Present': 'حاضر', 'Late': 'متأخر', 'Early Leave': 'انصراف مبكر', 'Absent': 'غائب',
        'Leave': 'إجازة', 'Holiday': 'عطلة', 'Weekly Rest': 'راحة أسبوعية', 'Work From Home': 'عمل من المنزل',
        'Mission': 'مأمورية', 'Training': 'دورة تدريب'
    };

    function badge(status) {
        var map = {
            'Present': 'badge-present', 'Late': 'badge-late', 'Early Leave': 'badge-absent', 'Absent': 'badge-absent',
            'Leave': 'badge-leave', 'Holiday': 'badge-holiday', 'Weekly Rest': 'badge-holiday', 'Work From Home': 'badge-leave'
        };
        var cls = map[status] || 'badge-present';
        var ar = statusArMap[status] || status;
        return '<span class="' + cls + '">' + ar + '</span>';
    }

    function clear(el) { el.innerHTML = ''; }

    function getAttQueryParams() {
        var emp = $('attEmployees').value.trim();
        var from = $('attFrom').value;
        var to = $('attTo').value;
        if (!from) { showError(currentLang === 'ar' ? 'يرجى اختيار تاريخ البداية' : 'Please select start date'); return null; }
        if (!to) { showError(currentLang === 'ar' ? 'يرجى اختيار تاريخ النهاية' : 'Please select end date'); return null; }
        var params = 'from=' + encodeURIComponent(from) + '&to=' + encodeURIComponent(to);
        if (emp) params += '&employees=' + encodeURIComponent(emp);
        return params;
    }

    document.querySelectorAll('.tab').forEach(function (tab) {
        tab.addEventListener('click', function () {
            document.querySelectorAll('.tab').forEach(function (t) { t.classList.remove('active'); });
            document.querySelectorAll('.tab-content').forEach(function (c) { c.classList.remove('active'); });
            this.classList.add('active');
            $(this.dataset.tab === 'attendance' ? 'tab-attendance' : 'tab-' + this.dataset.tab).classList.add('active');
        });
    });

    var today = new Date();
    $('attFrom').valueAsDate = today;
    $('attTo').valueAsDate = today;
    $('leaveFrom').valueAsDate = today;
    $('leaveTo').valueAsDate = today;
    $('leaveDays').value = 1;

    function updateLeaveDays() {
        var from = $('leaveFrom').value;
        var to = $('leaveTo').value;
        if (!from || !to) return;
        var start = new Date(from + 'T00:00:00');
        var end = new Date(to + 'T00:00:00');
        var days = Math.floor((end - start) / 86400000) + 1;
        $('leaveDays').value = days > 0 ? days : 0;
    }

    $('leaveFrom').addEventListener('change', updateLeaveDays);
    $('leaveTo').addEventListener('change', updateLeaveDays);
    updateLeaveDays();

    document.querySelectorAll('.choice-btn').forEach(function (btn) {
        btn.addEventListener('click', function () {
            document.querySelectorAll('.choice-btn').forEach(function (b) { b.classList.remove('active'); });
            this.classList.add('active');
            if (this.dataset.mode === 'pc') {
                document.querySelector('[data-tab="export"]').click();
            }
        });
    });

    fetch('/api/leave/types').then(function (r) { return r.json(); }).then(function (types) {
        var sel = $('leaveTypeId');
        types.forEach(function (t) {
            var opt = document.createElement('option');
            opt.value = t.id;
            opt.textContent = (t.nameAr || t.nameEn) + ' (' + t.code + ')';
            sel.appendChild(opt);
        });
    }).catch(function () {});

    // Attendance Tab
    $('fetchAttendanceBtn').addEventListener('click', function () {
        var params = getAttQueryParams();
        if (!params) return;

        showLoading();
        hideError();
        hide('attResults');
        hide('attExportActions');

        fetch('/api/tracking/query?' + params)
            .then(function (r) { if (!r.ok) throw new Error(currentLang === 'ar' ? 'فشل التحميل' : 'Failed to load'); return r.json(); })
            .then(function (data) {
                hideLoading();
                var el = $('attResultsContent');
                clear(el);

                if (!data || data.length === 0) {
                    el.innerHTML = '<p>' + (currentLang === 'ar' ? 'لا توجد بيانات بعد. جارٍ مزامنة بيانات البصمة تلقائياً...' : 'No data yet. Fingerprint data is being synced automatically...') + '</p>';
                    show('attResults');
                    return;
                }

                var html = '<table><thead><tr><th>#</th><th>' + (currentLang === 'ar' ? 'الرقم المالي' : 'Financial No') + '</th><th>' + (currentLang === 'ar' ? 'الاسم' : 'Name') + '</th><th>' + (currentLang === 'ar' ? 'الحالة' : 'Status') + '</th><th>' + (currentLang === 'ar' ? 'اليوم' : 'Day') + '</th><th>' + (currentLang === 'ar' ? 'التاريخ' : 'Date') + '</th><th>' + (currentLang === 'ar' ? 'الحضور' : 'Check In') + '</th><th>' + (currentLang === 'ar' ? 'الانصراف' : 'Check Out') + '</th><th>' + (currentLang === 'ar' ? 'المدة' : 'Duration') + '</th><th>' + (currentLang === 'ar' ? 'الموعد' : 'Schedule') + '</th></tr></thead><tbody>';
                data.forEach(function (r, i) {
                    var duration = '-';
                    if (r.firstPunch && r.lastPunch) {
                        var f = new Date(r.firstPunch);
                        var l = new Date(r.lastPunch);
                        var diffMin = Math.round((l - f) / 60000);
                        var dh = Math.floor(diffMin / 60);
                        var dm = diffMin % 60;
                        duration = dh + ' س ' + dm + ' د';
                    }
                    html += '<tr>' +
                        '<td>' + (i + 1) + '</td>' +
                        '<td>' + r.employeeFinancialNo + '</td>' +
                        '<td style="text-align:right;font-weight:600">' + r.employeeName + '</td>' +
                        '<td>' + badge(r.status) + '</td>' +
                        '<td class="day-name">' + fmtDayNameAr(r.dateDisplay) + '</td>' +
                        '<td>' + fmtDateAr(r.dateDisplay) + '</td>' +
                        '<td class="time-cell">' + fmtTimeAr(r.firstPunch) + '</td>' +
                        '<td class="time-cell">' + fmtTimeAr(r.lastPunch) + '</td>' +
                        '<td class="duration">' + duration + '</td>' +
                        '<td class="schedule-cell">' + fmtScheduleAr(r.scheduledStart) + ' - ' + fmtScheduleAr(r.scheduledEnd) + '</td>' +
                        '</tr>';
                });
                html += '</tbody></table>';
                el.innerHTML = html;
                show('attResults');
                show('attExportActions');
            })
            .catch(function (err) {
                hideLoading();
                showError(err.message);
            });
    });

    $('exportExcelBtn').addEventListener('click', function () {
        var params = getAttQueryParams();
        if (!params) return;
        window.open('/api/tracking/export/excel?' + params, '_blank');
    });

    $('exportPdfBtn').addEventListener('click', function () {
        var params = getAttQueryParams();
        if (!params) return;
        window.open('/api/tracking/export/pdf?' + params, '_blank');
    });

    // Employees Tab
    $('searchEmpBtn').addEventListener('click', function () {
        var q = $('empSearch').value.trim();
        if (!q) { showError(currentLang === 'ar' ? 'يرجى إدخال الاسم أو الرقم المالي' : 'Please enter name or financial number'); return; }

        showLoading();
        hideError();
        hide('empResults');

        fetch('/api/employees?search=' + encodeURIComponent(q))
            .then(function (r) { return r.json(); })
            .then(function (data) {
                hideLoading();
                var el = $('empResultsContent');
                clear(el);

                if (!data || data.length === 0) {
                    el.innerHTML = '<p>' + (currentLang === 'ar' ? 'لا توجد نتائج' : 'No results found') + '</p>';
                    show('empResults');
                    return;
                }

                var html = '<table><thead><tr><th>#</th><th>' + (currentLang === 'ar' ? 'الرقم المالي' : 'Financial No') + '</th><th>' + (currentLang === 'ar' ? 'الاسم' : 'Name') + '</th><th>' + (currentLang === 'ar' ? 'الوظيفة' : 'Job Title') + '</th><th>' + (currentLang === 'ar' ? 'المستوى' : 'Level') + '</th><th>' + (currentLang === 'ar' ? 'الإدارة' : 'Department') + '</th></tr></thead><tbody>';
                data.forEach(function (r, i) {
                    html += '<tr>' +
                        '<td>' + (i + 1) + '</td>' +
                        '<td>' + r.financialNo + '</td>' +
                        '<td style="text-align:right">' + r.name + '</td>' +
                        '<td>' + (r.jobTitle || '-') + '</td>' +
                        '<td>' + (r.level || '-') + '</td>' +
                        '<td>' + (r.department || '-') + '</td>' +
                        '</tr>';
                });
                html += '</tbody></table>';
                el.innerHTML = html;
                show('empResults');
            })
            .catch(function (err) {
                hideLoading();
                showError(err.message);
            });
    });

    $('empSearch').addEventListener('keyup', function (e) {
        if (e.key === 'Enter') $('searchEmpBtn').click();
    });

    // Employees Upload
    $('uploadEmployeesBtn').addEventListener('click', function () {
        var fileInput = $('employeesUploadFile');
        if (!fileInput.files || !fileInput.files.length) {
            showError(currentLang === 'ar' ? 'يرجى اختيار ملف' : 'Please select a file');
            return;
        }

        showLoading();
        hideError();
        hide('empResults');

        var fd = new FormData();
        fd.append('file', fileInput.files[0]);

        fetch('/api/employees/upload', { method: 'POST', body: fd })
            .then(function (r) { return r.json(); })
            .then(function (data) {
                hideLoading();
                var el = $('empResultsContent');
                el.innerHTML = '<div class="result-card"><div class="emp-header">' + (currentLang === 'ar' ? 'تم الرفع بنجاح' : 'Upload successful') + '</div><p>' + (data.message || JSON.stringify(data)) + '</p></div>';
                show('empResults');
            })
            .catch(function (err) {
                hideLoading();
                showError(err.message);
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
                var el = $('balResultsContent');
                clear(el);

                if (!data || data.length === 0) {
                    el.innerHTML = '<p>' + (currentLang === 'ar' ? 'لا توجد أرصدة' : 'No balances found') + '</p>';
                    show('balResults');
                    return;
                }

                var html = '<table><thead><tr><th>' + (currentLang === 'ar' ? 'السنة' : 'Year') + '</th><th>' + (currentLang === 'ar' ? 'الرقم المالي' : 'Financial No') + '</th><th>' + (currentLang === 'ar' ? 'الاسم' : 'Name') + '</th><th>' + (currentLang === 'ar' ? 'اعتيادى' : 'Regular') + '</th><th>' + (currentLang === 'ar' ? 'عارضه' : 'Casual') + '</th><th>' + (currentLang === 'ar' ? 'بدل راحه' : 'Rest Allow.') + '</th><th>' + (currentLang === 'ar' ? 'بدل عطله' : 'Holiday Allow.') + '</th></tr></thead><tbody>';
                data.forEach(function (b) {
                    html += '<tr><td>' + b.year + '</td><td>' + (b.employeeFinancialNo || (b.employee ? b.employee.financialNo : '')) + '</td><td>' + (b.employee ? b.employee.name : '') + '</td><td>' + b.regularLeave + '</td><td>' + b.casualLeave + '</td><td>' + b.restAllowance + '</td><td>' + b.holidayAllowance + '</td></tr>';
                });
                html += '</tbody></table>';
                el.innerHTML = html;
                show('balResults');
            })
            .catch(function (err) {
                hideLoading();
                showError(err.message);
            });
    });

    $('balFinNo').addEventListener('keyup', function (e) {
        if (e.key === 'Enter') $('fetchBalBtn').click();
    });

    // Balances Upload
    $('uploadBalancesBtn').addEventListener('click', function () {
        var fileInput = $('balancesUploadFile');
        if (!fileInput.files || !fileInput.files.length) {
            showError(currentLang === 'ar' ? 'يرجى اختيار ملف' : 'Please select a file');
            return;
        }

        showLoading();
        hideError();
        hide('balResults');

        var fd = new FormData();
        fd.append('file', fileInput.files[0]);

        fetch('/api/tracking/balances/upload', { method: 'POST', body: fd })
            .then(function (r) { return r.json(); })
            .then(function (data) {
                hideLoading();
                var el = $('balResultsContent');
                el.innerHTML = '<div class="result-card"><div class="emp-header">' + (currentLang === 'ar' ? 'تم الرفع بنجاح' : 'Upload successful') + '</div><p>' + (data.message || JSON.stringify(data)) + '</p></div>';
                show('balResults');
            })
            .catch(function (err) {
                hideLoading();
                showError(err.message);
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
        if (!fromDate || !toDate) { showError(currentLang === 'ar' ? 'يرجى اختيار تاريخ البداية والنهاية' : 'Please select start and end dates'); return; }
        updateLeaveDays();
        if (parseFloat($('leaveDays').value) <= 0) { showError(currentLang === 'ar' ? 'تاريخ النهاية يجب أن يكون بعد أو يساوي تاريخ البداية' : 'End date must be after or equal to start date'); return; }

        showLoading();
        hideError();
        hide('leaveResults');

        fetch('/api/leave/grant', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                financialNo: finNo,
                leaveTypeId: parseInt(leaveTypeId),
                fromDate: fromDate,
                toDate: toDate,
                daysCount: 0,
                reason: reason || null
            })
        })
            .then(function (r) {
                if (!r.ok) return r.json().then(function (e) { throw new Error(e.error); });
                return r.json();
            })
            .then(function (data) {
                hideLoading();
                var el = $('leaveResultsContent');
                el.innerHTML = '<div class="result-card">' +
                    '<div class="emp-header">' + (currentLang === 'ar' ? 'تم منح الإجازة بنجاح' : 'Leave granted successfully') + '</div>' +
                    '<div class="punch-row"><span class="punch-label">' + (currentLang === 'ar' ? 'من:' : 'From:') + '</span><span class="punch-value">' + fmtDate(data.fromDate) + '</span></div>' +
                    '<div class="punch-row"><span class="punch-label">' + (currentLang === 'ar' ? 'إلى:' : 'To:') + '</span><span class="punch-value">' + fmtDate(data.toDate) + '</span></div>' +
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

    $('fetchLeaveTransBtn').addEventListener('click', function () {
        showLoading();
        hideError();
        hide('leaveResults');

        var url = '/api/leave/transactions' + buildLeaveFilterQuery();
        fetch(url)
            .then(function (r) { return r.json(); })
            .then(function (data) {
                hideLoading();
                var el = $('leaveResultsContent');
                clear(el);

                if (!data || data.length === 0) {
                    el.innerHTML = '<p>' + (currentLang === 'ar' ? 'لا توجد سجلات إجازات' : 'No leave records found') + '</p>';
                    show('leaveResults');
                    $('leaveExportActions').style.display = 'none';
                    return;
                }

                var html = '<table><thead><tr><th>#</th><th>' + (currentLang === 'ar' ? 'الرقم المالي' : 'Financial No') + '</th><th>' + (currentLang === 'ar' ? 'الموظف' : 'Employee') + '</th><th>' + (currentLang === 'ar' ? 'النوع' : 'Type') + '</th><th>' + (currentLang === 'ar' ? 'من' : 'From') + '</th><th>' + (currentLang === 'ar' ? 'إلى' : 'To') + '</th><th>' + (currentLang === 'ar' ? 'أيام' : 'Days') + '</th><th>' + (currentLang === 'ar' ? 'السبب' : 'Reason') + '</th><th>' + (currentLang === 'ar' ? 'التاريخ' : 'Date') + '</th></tr></thead><tbody>';
                data.forEach(function (t, i) {
                    html += '<tr>' +
                        '<td>' + (i + 1) + '</td>' +
                        '<td>' + t.employeeFinancialNo + '</td>' +
                        '<td>' + (t.employee ? t.employee.name : '-') + '</td>' +
                        '<td>' + (t.leaveType ? (t.leaveType.nameAr || t.leaveType.nameEn) : '') + '</td>' +
                        '<td>' + fmtDate(t.fromDate) + '</td>' +
                        '<td>' + fmtDate(t.toDate) + '</td>' +
                        '<td>' + t.daysCount + '</td>' +
                        '<td>' + (t.reason || '-') + '</td>' +
                        '<td>' + t.createdAt + '</td>' +
                        '</tr>';
                });
                html += '</tbody></table>';
                el.innerHTML = html;
                show('leaveResults');
                $('leaveExportActions').style.display = 'flex';
            })
            .catch(function (err) {
                hideLoading();
                showError(err.message);
            });
    });

    $('exportLeaveExcelBtn').addEventListener('click', function () {
        window.open('/api/leave/export/excel' + buildLeaveFilterQuery(), '_blank');
    });

    $('exportLeavePdfBtn').addEventListener('click', function () {
        window.open('/api/leave/export/pdf' + buildLeaveFilterQuery(), '_blank');
    });

    $('downloadLeaveTemplateBtn').addEventListener('click', function () {
        window.open('/api/leave/upload-template', '_blank');
    });

    $('downloadEmployeesTemplateBtn').addEventListener('click', function () {
        window.open('/api/import/template/employees', '_blank');
    });

    $('downloadBalancesTemplateBtn').addEventListener('click', function () {
        window.open('/api/import/template/balances', '_blank');
    });

    $('downloadMonthlyTemplateBtn').addEventListener('click', function () {
        window.open('/api/import/template/monthly', '_blank');
    });

    // Leaves Upload
    $('uploadLeavesBtn').addEventListener('click', function () {
        var fileInput = $('leavesUploadFile');
        if (!fileInput.files || !fileInput.files.length) {
            showError(currentLang === 'ar' ? 'يرجى اختيار ملف' : 'Please select a file');
            return;
        }

        showLoading();
        hideError();
        hide('leaveResults');

        var fd = new FormData();
        fd.append('file', fileInput.files[0]);

        fetch('/api/leave/upload', { method: 'POST', body: fd })
            .then(function (r) { return r.json(); })
            .then(function (data) {
                hideLoading();
                var el = $('leaveResultsContent');
                el.innerHTML = '<div class="result-card"><div class="emp-header">' + (currentLang === 'ar' ? 'تم الرفع بنجاح' : 'Upload successful') + '</div><p>' + (data.message || JSON.stringify(data)) + '</p></div>';
                show('leaveResults');
            })
            .catch(function (err) {
                hideLoading();
                showError(err.message);
            });
    });

    // Export Tab - Load departments
    fetch('/api/export/departments').then(function (r) { return r.json(); }).then(function (depts) {
        var sel = $('exportDept');
        depts.forEach(function (d) {
            var opt = document.createElement('option');
            opt.value = d;
            opt.textContent = d;
            sel.appendChild(opt);
        });
    }).catch(function () {});

    function getExportParams() {
        var year = $('exportYear').value;
        var month = $('exportMonth').value;
        var dept = $('exportDept').value;
        if (!year || !month) { showError(currentLang === 'ar' ? 'يرجى اختيار السنة والشهر' : 'Please select year and month'); return null; }
        var params = 'year=' + year + '&month=' + month;
        if (dept) params += '&department=' + encodeURIComponent(dept);
        return { params: params, year: year, month: month, dept: dept };
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
                var el = $('exportPreviewContent');
                clear(el);

                if (!data || data.length === 0) {
                    el.innerHTML = '<p>' + (currentLang === 'ar' ? 'لا توجد بيانات لهذا الشهر' : 'No data for this month') + '</p>';
                    show('exportPreview');
                    return;
                }

                var daysInMonth = new Date(parseInt(info.year), parseInt(info.month), 0).getDate();
                var monthNames = ['', 'يناير', 'فبراير', 'مارس', 'ابريل', 'مايو', 'يونيو', 'يوليو', 'اغسطس', 'سبتمبر', 'اكتوبر', 'نوفمبر', 'ديسمبر'];

                var html = '<div class="sayed-wrap"><table class="sayed-table">';

                html += '<caption>time sheet periood ' + monthNames[parseInt(info.month)] + ' ' + info.year + '</caption>';

                html += '<thead>';

                // Row 6: Arabic summary labels
                html += '<tr>';
                html += '<th colspan="3" style="background:#D9D9D9"></th>';
                html += '<th colspan="' + daysInMonth + '" style="background:#D9D9D9"></th>';
                html += '<th style="background:#D9D9D9">Total Working Days</th>';
                html += '<th style="background:#D9D9D9">Employee Signature</th>';
                html += '<th class="srow-ar">أيام<br>حضور</th>';
                html += '<th class="srow-ar">اجازة<br>إعتيادى</th>';
                html += '<th class="srow-ar">أيام<br>مرضى</th>';
                html += '<th class="srow-ar">أيام<br>عارضه</th>';
                html += '<th class="srow-ar">غياب</th>';
                html += '<th class="srow-ar">راحه</th>';
                html += '<th class="srow-ar">مأمورية<br>خارجية</th>';
                html += '<th class="srow-ar">مأمورية<br>داخلية</th>';
                html += '<th class="srow-ar">دورة<br>تدريب</th>';
                html += '<th style="background:#D9D9D9">الادارة العامة</th>';
                html += '<th style="background:#D9D9D9">المستوى الوظيفى</th>';
                html += '<th style="background:#D9D9D9">Box</th>';
                html += '</tr>';

                // Row 7: Main headers
                html += '<tr>';
                html += '<th>PR</th>';
                html += '<th>Name</th>';
                html += '<th>Job Title</th>';
                for (var d = 1; d <= daysInMonth; d++) {
                    var dow = new Date(parseInt(info.year), parseInt(info.month) - 1, d).getDay();
                    var cls = (dow === 5 || dow === 6) ? ' class="sday-gray"' : '';
                    html += '<th' + cls + '>' + d + '</th>';
                }
                html += '<th>Total Working Days</th>';
                html += '<th>Employee Signature</th>';
                html += '<th>X</th><th>A</th><th>S</th><th>C</th><th>B</th><th>E</th><th>DI</th><th>DX</th><th>T</th>';
                html += '<th>الادارة العامة</th>';
                html += '<th>المستوى الوظيفى</th>';
                html += '<th>Box</th>';
                html += '</tr>';
                html += '</thead><tbody>';

                // Data rows
                data.forEach(function (emp, i) {
                    html += '<tr>';
                    html += '<td>' + emp.financialNo + '</td>';
                    html += '<td class="name-cell">' + (emp.name || '') + '</td>';
                    html += '<td class="job-cell">' + (emp.jobTitle || '') + '</td>';

                    var presentDays = 0, regLeave = 0, sickDays = 0, casualDays = 0;
                    var absenceDays = 0, restDays = 0, extMission = 0, intMission = 0, trainingDays = 0;

                    for (var d = 1; d <= daysInMonth; d++) {
                        var code = (emp.dailyCodes && emp.dailyCodes[d - 1]) ? emp.dailyCodes[d - 1] : '';
                        var dow = new Date(parseInt(info.year), parseInt(info.month) - 1, d).getDay();
                        var cellCls = 'code-cell';
                        if (dow === 5 || dow === 6) cellCls += ' sday-gray';
                        html += '<td class="' + cellCls + '">' + code + '</td>';

                        if (code) {
                            var cu = code.toUpperCase();
                            if (cu === 'R' || cu === 'WH' || cu === 'X' || cu === 'X1' || cu === 'X2' || cu === 'X3' || cu === 'X4') presentDays++;
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

                    html += '<td class="total-cell">' + presentDays + '</td>';
                    html += '<td></td>';
                    html += '<td class="count-cell">' + presentDays + '</td>';
                    html += '<td class="count-cell">' + regLeave + '</td>';
                    html += '<td class="count-cell">' + sickDays + '</td>';
                    html += '<td class="count-cell">' + casualDays + '</td>';
                    html += '<td class="count-cell">' + absenceDays + '</td>';
                    html += '<td class="count-cell">' + restDays + '</td>';
                    html += '<td class="count-cell">' + extMission + '</td>';
                    html += '<td class="count-cell">' + intMission + '</td>';
                    html += '<td class="count-cell">' + trainingDays + '</td>';
                    html += '<td class="dept-cell">' + (emp.department || '') + '</td>';
                    html += '<td>' + (emp.level || '') + '</td>';
                    html += '<td></td>';
                    html += '</tr>';
                });

                html += '</tbody></table></div>';
                el.innerHTML = html;
                show('exportPreview');
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

    $('downloadMonthlyExcelTopBtn').addEventListener('click', function () {
        downloadMonthly('excel');
    });

    $('downloadMonthlyPdfTopBtn').addEventListener('click', function () {
        downloadMonthly('pdf');
    });

    function downloadMonthly(type) {
        var info = getExportParams();
        if (!info) return;
        var path = type === 'pdf' ? '/api/export/monthly/pdf?' : '/api/export/monthly?';
        window.open(path + info.params, '_blank');
    }

    // Monthly Attendance Upload
    $('uploadMonthlyAttendanceBtn').addEventListener('click', function () {
        var fileInput = $('monthlyAttendanceUploadFile');
        if (!fileInput.files || !fileInput.files.length) {
            showError(currentLang === 'ar' ? 'يرجى اختيار ملف' : 'Please select a file');
            return;
        }

        showLoading();
        hideError();
        hide('exportResults');

        var fd = new FormData();
        fd.append('file', fileInput.files[0]);

        fetch('/api/export/monthly/upload', { method: 'POST', body: fd })
            .then(function (r) { return r.json(); })
            .then(function (data) {
                hideLoading();
                var el = $('exportResultsContent');
                el.innerHTML = '<div class="result-card"><div class="emp-header">' + (currentLang === 'ar' ? 'تم الرفع بنجاح' : 'Upload successful') + '</div><p>' + (data.message || JSON.stringify(data)) + '</p></div>';
                show('exportResults');
            })
            .catch(function (err) {
                hideLoading();
                showError(err.message);
            });
    });

    function updateSyncStatus() {
        fetch('/api/import/sync-status')
            .then(function (r) { return r.json(); })
            .then(function (data) {
                var el = $('syncStatus');
                if (!el) return;
                if (!data.lastSyncTime) {
                    el.innerHTML = '<span style="color:#f59e0b;">&#9679;</span> ' + (currentLang === 'ar' ? 'جارٍ الاتصال بجهاز البصمة...' : 'Connecting to fingerprint device...');
                    return;
                }
                var time = new Date(data.lastSyncTime);
                var h = String(time.getHours()).padStart(2, '0');
                var m = String(time.getMinutes()).padStart(2, '0');
                var s = String(time.getSeconds()).padStart(2, '0');
                var color = data.isRunning ? '#188038' : '#666';
                var label = currentLang === 'ar' ? 'آخر مزامنة:' : 'Last sync:';
                el.innerHTML = '<span style="color:' + color + ';">&#9679;</span> ' + label + ' ' + h + ':' + m + ':' + s;
            })
            .catch(function () {
                var el = $('syncStatus');
                if (!el) return;
                var msg = currentLang === 'ar' ? 'تعذر الاتصال بالخادم' : 'Failed to connect to server';
                el.innerHTML = '<span style="color:#dc3545;">&#9679;</span> ' + msg;
            });
    }

    updateSyncStatus();
    setInterval(updateSyncStatus, 10000);

    applyLanguage();
})();
