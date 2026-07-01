(function () {
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
        if (!from) { showError('يرجى اختيار تاريخ البداية'); return null; }
        if (!to) { showError('يرجى اختيار تاريخ النهاية'); return null; }
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
            .then(function (r) { if (!r.ok) throw new Error('فشل التحميل'); return r.json(); })
            .then(function (data) {
                hideLoading();
                var el = $('attResultsContent');
                clear(el);

                if (!data || data.length === 0) {
                    el.innerHTML = '<p>لا توجد بيانات بعد. جارٍ مزامنة بيانات البصمة تلقائياً...</p>';
                    show('attResults');
                    return;
                }

                var html = '<table><thead><tr><th>#</th><th>الرقم المالي</th><th>الاسم</th><th>الحالة</th><th>اليوم</th><th>التاريخ</th><th>الحضور</th><th>الانصراف</th><th>المدة</th><th>الموعد</th></tr></thead><tbody>';
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
        if (!q) { showError('يرجى إدخال الاسم أو الرقم المالي'); return; }

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
                    el.innerHTML = '<p>لا توجد نتائج</p>';
                    show('empResults');
                    return;
                }

                var html = '<table><thead><tr><th>#</th><th>الرقم المالي</th><th>الاسم</th><th>الوظيفة</th><th>المستوى</th><th>الإدارة</th></tr></thead><tbody>';
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

    // Balances Tab
    $('fetchBalBtn').addEventListener('click', function () {
        var finNo = $('balFinNo').value.trim();
        if (!finNo) { showError('يرجى إدخال الرقم المالي'); return; }

        showLoading();
        hideError();
        hide('balResults');

        fetch('/api/tracking/' + encodeURIComponent(finNo) + '/balances')
            .then(function (r) { return r.json(); })
            .then(function (data) {
                hideLoading();
                var el = $('balResultsContent');
                clear(el);

                if (!data || data.length === 0) {
                    el.innerHTML = '<p>لا توجد أرصدة لهذا الموظف</p>';
                    show('balResults');
                    return;
                }

                var html = '<table><thead><tr><th>السنة</th><th>اعتيادى</th><th>عارضه</th><th>بدل راحه</th><th>بدل عطله</th></tr></thead><tbody>';
                data.forEach(function (b) {
                    html += '<tr><td>' + b.year + '</td><td>' + b.regularLeave + '</td><td>' + b.casualLeave + '</td><td>' + b.restAllowance + '</td><td>' + b.holidayAllowance + '</td></tr>';
                });
                html += '</tbody></table>';
                el.innerHTML = html;

                if (data[0].employee) {
                    var emp = data[0].employee;
                    el.innerHTML += '<div style="margin-top:12px;padding:12px;background:#f5f8ff;border-radius:4px;">' +
                        '<strong>' + emp.name + '</strong> - ' + (emp.jobTitle || '') + '<br>' +
                        'الإدارة: ' + (emp.department || '-') + ' | المستوى: ' + (emp.level || '-') +
                        '</div>';
                }

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

    // Leave Tab
    $('grantLeaveBtn').addEventListener('click', function () {
        var finNo = $('leaveFinNo').value.trim();
        var leaveTypeId = $('leaveTypeId').value;
        var fromDate = $('leaveFrom').value;
        var toDate = $('leaveTo').value;
        var reason = $('leaveReason').value.trim();

        if (!finNo) { showError('يرجى إدخال الرقم المالي'); return; }
        if (!fromDate || !toDate) { showError('يرجى اختيار تاريخ البداية والنهاية'); return; }
        updateLeaveDays();
        if (parseFloat($('leaveDays').value) <= 0) { showError('تاريخ النهاية يجب أن يكون بعد أو يساوي تاريخ البداية'); return; }

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
                    '<div class="emp-header">تم منح الإجازة بنجاح</div>' +
                    '<div class="punch-row"><span class="punch-label">من:</span><span class="punch-value">' + fmtDate(data.fromDate) + '</span></div>' +
                    '<div class="punch-row"><span class="punch-label">إلى:</span><span class="punch-value">' + fmtDate(data.toDate) + '</span></div>' +
                    '<div class="punch-row"><span class="punch-label">عدد الأيام:</span><span class="punch-value">' + data.daysCount + '</span></div>' +
                    '</div>';
                show('leaveResults');
            })
            .catch(function (err) {
                hideLoading();
                showError(err.message);
            });
    });

    $('fetchLeaveTransBtn').addEventListener('click', function () {
        var finNo = $('leaveFinNo').value.trim();

        showLoading();
        hideError();
        hide('leaveResults');

        var url = finNo ? '/api/leave/transactions?financialNo=' + encodeURIComponent(finNo) : '/api/leave/transactions';
        fetch(url)
            .then(function (r) { return r.json(); })
            .then(function (data) {
                hideLoading();
                var el = $('leaveResultsContent');
                clear(el);

                if (!data || data.length === 0) {
                    el.innerHTML = '<p>لا توجد سجلات إجازات</p>';
                    show('leaveResults');
                    return;
                }

                var html = '<table><thead><tr><th>#</th><th>الرقم المالي</th><th>الموظف</th><th>النوع</th><th>من</th><th>إلى</th><th>أيام</th><th>السبب</th><th>التاريخ</th></tr></thead><tbody>';
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
            })
            .catch(function (err) {
                hideLoading();
                showError(err.message);
            });
    });

    $('downloadLeaveTemplateBtn').addEventListener('click', function () {
        window.open('/api/leave/upload-template', '_blank');
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
        if (!year || !month) { showError('يرجى اختيار السنة والشهر'); return null; }
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
            .then(function (r) { if (!r.ok) throw new Error('فشل التحميل'); return r.json(); })
            .then(function (data) {
                hideLoading();
                var el = $('exportPreviewContent');
                clear(el);

                if (!data || data.length === 0) {
                    el.innerHTML = '<p>لا توجد بيانات لهذا الشهر</p>';
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

    function updateSyncStatus() {
        fetch('/api/import/sync-status')
            .then(function (r) { return r.json(); })
            .then(function (data) {
                var el = $('syncStatus');
                if (!data.lastSyncTime) {
                    el.innerHTML = '<span style="color:#f59e0b;">&#9679;</span> جارٍ الاتصال بجهاز البصمة...';
                    return;
                }
                var time = new Date(data.lastSyncTime);
                var h = String(time.getHours()).padStart(2, '0');
                var m = String(time.getMinutes()).padStart(2, '0');
                var s = String(time.getSeconds()).padStart(2, '0');
                var dot = data.isRunning ? '&#9679;' : '&#9679;';
                var color = data.isRunning ? '#188038' : '#666';
                el.innerHTML = '<span style="color:' + color + ';">' + dot + '</span> آخر مزامنة: ' + h + ':' + m + ':' + s;
            })
            .catch(function () {
                var el = $('syncStatus');
                el.innerHTML = '<span style="color:#dc3545;">&#9679;</span> تعذر الاتصال بالخادم';
            });
    }

    updateSyncStatus();
    setInterval(updateSyncStatus, 10000);
})();
