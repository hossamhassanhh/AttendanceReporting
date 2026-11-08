using AttendanceApp.Data;
using AttendanceApp.Models;
using ClosedXML.Excel;
using ExcelDataReader;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AttendanceApp.Services;

public class LeaveApprovalWorkflowService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public LeaveApprovalWorkflowService(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public async Task<byte[]> GenerateApprovalWorkflowTemplateAsync(bool isArabic)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(isArabic ? "سير عمل الاعتماد" : "Approval Workflow");
        sheet.RightToLeft = isArabic;

        sheet.Cell(1, 1).Value = isArabic
            ? "سير عمل اعتماد الإجازات: لكل موظف مندوب (اختياري) + لكل إدارة مدير ومندوب مدير (اختياري) وموظف موارد بشرية. الدورة: موظف -> مندوب الموظف -> المدير/مندوب المدير -> موظف الموارد البشرية."
            : "Leave approval workflow: employee delegate (optional) + per department manager, manager delegate (optional), and HR employee. Cycle: Employee -> Employee Delegate -> Manager/Manager Delegate -> HR Employee.";
        sheet.Range(1, 1, 1, 8).Merge();
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Alignment.WrapText = true;

        var headers = isArabic
            ? new[] { "الإدارة", "الرقم المالي للموظف", "اسم الموظف", "مندوب الموظف (الرقم المالي)", "مدير الإدارة (الرقم المالي)", "مندوب المدير (الرقم المالي)", "موظف الموارد البشرية (الرقم المالي)", "ملاحظات" }
            : new[] { "Department", "EmployeeFinancialNo", "EmployeeName", "EmployeeDelegateFinancialNo", "DeptManagerFinancialNo", "DeptManagerDelegateFinancialNo", "DeptHrFinancialNo", "Notes" };

        for (var i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(2, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#d9e1f2");
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        }

        // Sample rows
        var samples = new[]
        {
            new[] { "IT", "1039", "Employee 1039", "1087", "693", "1110", "1182", isArabic ? "مثال: مندوب + مدير + مندوب مدير + موارد بشرية" : "Example: delegate + manager + manager delegate + HR" },
            new[] { "IT", "1087", "Employee 1087", "", "693", "1110", "1182", isArabic ? "مثال: بدون مندوب للموظف" : "Example: no employee delegate" },
            new[] { "IT", "1110", "Employee 1110", "", "693", "1110", "1182", isArabic ? "مندوب المدير (قد لا يحتاج مندوب)" : "Manager delegate (may have no delegate)" },
        };
        for (var r = 0; r < samples.Length; r++)
        {
            for (var c = 0; c < samples[r].Length; c++)
                sheet.Cell(r + 3, c + 1).Value = samples[r][c];
        }

        sheet.Columns().AdjustToContents();
        for (var c = 1; c <= headers.Length; c++) if (sheet.Column(c).Width > 35) sheet.Column(c).Width = 35;

        // Instructions
        var inst = workbook.Worksheets.Add(isArabic ? "تعليمات" : "Instructions");
        inst.RightToLeft = isArabic;
        inst.Cell(1, 1).Value = isArabic
            ? "تعليمات سير العمل:\n1) الدورة: يقدم الموظف -> يوافق مندوب الموظف (إن وجد) -> يوافق المدير أو مندوب المدير (إن وجد أحدهما) -> يوافق موظف الموارد البشرية بالإدارة.\n2) مندوب الموظف: عمود EmployeeDelegateFinancialNo (اختياري لكل موظف). إذا كان فارغاً تُتخطى هذه الخطوة.\n3) مدير الإدارة ومندوب المدير: أعمدة DeptManagerFinancialNo و DeptManagerDelegateFinancialNo (على مستوى الإدارة، يؤخذ أول قيمة غير فارغة لكل إدارة). يمكن لأي منهما الاعتماد في خطوة المدير.\n4) موظف الموارد البشرية: عمود DeptHrFinancialNo (على مستوى الإدارة).\n5) عند الاستيراد: يتم تحديث مندوب كل موظف وتحديث إعدادات كل إدارة."
            : "Workflow instructions:\n1) Cycle: Employee submits -> Employee delegate approves (if any) -> Manager or Manager Delegate approves (if any) -> Department HR employee approves.\n2) Employee delegate: EmployeeDelegateFinancialNo column (optional per employee). If empty, this step is skipped.\n3) Department manager & manager delegate: DeptManagerFinancialNo and DeptManagerDelegateFinancialNo (department-level; first non-empty value per department is used). Either can approve at the manager step.\n4) Department HR: DeptHrFinancialNo (department-level).\n5) On import: each employee's delegate is updated and each department's workflow config is upserted.";
        inst.Cell(1, 1).Style.Alignment.WrapText = true;
        inst.Column(1).Width = 100;

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<string> ImportApprovalWorkflowAsync(string filePath, string enteredBy)
    {
        if (string.IsNullOrWhiteSpace(enteredBy))
            throw new ArgumentException("Authenticated username is required", nameof(enteredBy));
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Workflow file not found", filePath);

        using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = false }
        });
        if (dataSet.Tables.Count == 0)
            return "No worksheets found in the workflow file.";

        var table = dataSet.Tables[0];
        var (headerRow, col) = FindHeaderAndMap(table);
        if (headerRow < 0 || (col.EmployeeFinancialNo < 0 && col.Department < 0))
            return "Required columns not found (need Department and/or EmployeeFinancialNo).";

        using var db = await _factory.CreateDbContextAsync();
        int empUpdated = 0, deptUpdated = 0, skipped = 0;
        var deptConfigs = new Dictionary<string, (string? mgr, string? mgrDel, string? hr)>(StringComparer.OrdinalIgnoreCase);

        for (int r = headerRow + 1; r < table.Rows.Count; r++)
        {
            var row = table.Rows[r];
            var dept = Get(row, col.Department);
            var empFin = Get(row, col.EmployeeFinancialNo);
            var empDelegate = Get(row, col.EmployeeDelegateFinancialNo);
            var deptMgr = Get(row, col.DeptManagerFinancialNo);
            var deptMgrDel = Get(row, col.DeptManagerDelegateFinancialNo);
            var deptHr = Get(row, col.DeptHrFinancialNo);

            // Skip repeated header rows
            if (IsHeaderLike(dept) || IsHeaderLike(empFin)) continue;
            if (string.IsNullOrWhiteSpace(dept) && string.IsNullOrWhiteSpace(empFin))
            {
                skipped++; continue;
            }

            // Update employee delegate
            if (!string.IsNullOrWhiteSpace(empFin))
            {
                var emp = await db.Employees.FirstOrDefaultAsync(e => e.FinancialNo == empFin);
                if (emp != null)
                {
                    emp.DelegateFinancialNo = string.IsNullOrWhiteSpace(empDelegate) ? null : empDelegate;
                    empUpdated++;
                }
                else skipped++;
            }

            // Collect department config (first non-empty wins per field)
            if (!string.IsNullOrWhiteSpace(dept))
            {
                if (!deptConfigs.TryGetValue(dept, out var cfg)) cfg = (null, null, null);
                if (string.IsNullOrWhiteSpace(cfg.mgr) && !string.IsNullOrWhiteSpace(deptMgr)) cfg.mgr = deptMgr;
                if (string.IsNullOrWhiteSpace(cfg.mgrDel) && !string.IsNullOrWhiteSpace(deptMgrDel)) cfg.mgrDel = deptMgrDel;
                if (string.IsNullOrWhiteSpace(cfg.hr) && !string.IsNullOrWhiteSpace(deptHr)) cfg.hr = deptHr;
                deptConfigs[dept] = cfg;
            }
        }

        foreach (var kv in deptConfigs)
        {
            if (string.IsNullOrWhiteSpace(kv.Value.mgr) && string.IsNullOrWhiteSpace(kv.Value.mgrDel) && string.IsNullOrWhiteSpace(kv.Value.hr))
                continue;
            var existing = await db.DepartmentWorkflowConfigs.FirstOrDefaultAsync(c => c.Department == kv.Key);
            if (existing == null)
            {
                db.DepartmentWorkflowConfigs.Add(new DepartmentWorkflowConfig
                {
                    Department = kv.Key,
                    ManagerFinancialNo = kv.Value.mgr,
                    ManagerDelegateFinancialNo = kv.Value.mgrDel,
                    HrFinancialNo = kv.Value.hr,
                    UpdatedAt = DateTime.Now
                });
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(kv.Value.mgr)) existing.ManagerFinancialNo = kv.Value.mgr;
                if (!string.IsNullOrWhiteSpace(kv.Value.mgrDel)) existing.ManagerDelegateFinancialNo = kv.Value.mgrDel;
                if (!string.IsNullOrWhiteSpace(kv.Value.hr)) existing.HrFinancialNo = kv.Value.hr;
                existing.UpdatedAt = DateTime.Now;
            }
            deptUpdated++;
        }

        await db.SaveChangesAsync();
        return $"Workflow imported by {enteredBy}: {empUpdated} employee delegate(s) updated, {deptUpdated} department config(s) updated, {skipped} row(s) skipped.";
    }

    private static (int headerRow, (int Department, int EmployeeFinancialNo, int EmployeeName, int EmployeeDelegateFinancialNo, int DeptManagerFinancialNo, int DeptManagerDelegateFinancialNo, int DeptHrFinancialNo) col) FindHeaderAndMap(DataTable table)
    {
        var empty = (Department: -1, EmployeeFinancialNo: -1, EmployeeName: -1, EmployeeDelegateFinancialNo: -1, DeptManagerFinancialNo: -1, DeptManagerDelegateFinancialNo: -1, DeptHrFinancialNo: -1);
        for (int r = 0; r < Math.Min(table.Rows.Count, 10); r++)
        {
            var map = BuildColumnMap(table.Rows[r], table.Columns.Count);
            if (map.Department >= 0 || map.EmployeeFinancialNo >= 0)
                return (r, map);
        }
        return (-1, empty);
    }

    private static (int Department, int EmployeeFinancialNo, int EmployeeName, int EmployeeDelegateFinancialNo, int DeptManagerFinancialNo, int DeptManagerDelegateFinancialNo, int DeptHrFinancialNo) BuildColumnMap(DataRow headerRow, int colCount)
    {
        int Find(params string[] names)
        {
            for (int i = 0; i < colCount; i++)
            {
                var n = headerRow[i]?.ToString()?.Trim() ?? "";
                if (string.IsNullOrEmpty(n)) continue;
                if (names.Any(x => string.Equals(x, n, StringComparison.OrdinalIgnoreCase))) return i;
                var nn = n.Replace(" ", "").Replace("(", "").Replace(")", "");
                if (names.Any(x => string.Equals(x.Replace(" ", "").Replace("(", "").Replace(")", ""), nn, StringComparison.OrdinalIgnoreCase))) return i;
            }
            return -1;
        }
        return (
            Find("Department", "الإدارة"),
            Find("EmployeeFinancialNo", "FinancialNo", "الرقم المالي للموظف", "رقم الموظف المالي"),
            Find("EmployeeName", "Employee Name", "اسم الموظف"),
            Find("EmployeeDelegateFinancialNo", "EmployeeDelegate", "مندوب الموظف (الرقم المالي)", "مندوب الموظف", "المندوب الرئيسي"),
            Find("DeptManagerFinancialNo", "Manager", "DeptManager", "مدير الإدارة (الرقم المالي)", "مدير الإدارة", "المدير"),
            Find("DeptManagerDelegateFinancialNo", "ManagerDelegate", "DeptManagerDelegate", "مندوب المدير (الرقم المالي)", "مندوب المدير"),
            Find("DeptHrFinancialNo", "HrFinancialNo", "DeptHr", "HR", "موظف الموارد البشرية (الرقم المالي)", "موظف الموارد البشرية", "الموارد البشرية")
        );
    }

    private static bool IsHeaderLike(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return value.Equals("Department", StringComparison.OrdinalIgnoreCase)
            || value.Equals("EmployeeFinancialNo", StringComparison.OrdinalIgnoreCase)
            || value.Equals("الإدارة", StringComparison.OrdinalIgnoreCase)
            || value.Equals("الرقم المالي للموظف", StringComparison.OrdinalIgnoreCase);
    }

    private static string Get(DataRow row, int idx)
    {
        if (idx < 0 || idx >= row.Table.Columns.Count) return "";
        var v = row[idx];
        if (v == null || v == DBNull.Value) return "";
        if (v is double d) return d % 1 == 0 ? ((long)d).ToString() : d.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return v.ToString()?.Trim() ?? "";
    }
}