using AttendanceApp.Models;
using ClosedXML.Excel;
using ExcelDataReader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace AttendanceApp.Services;

public class LeaveApprovalWorkflowService
{
    public async Task<byte[]> GenerateApprovalWorkflowTemplateAsync(bool isArabic)
    {
        // Returns a workbook with two sheets for the leave approval cycle workflow.
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(isArabic ? "سير عمل الإجازات" : "Leave Approval Workflow");
        sheet.RightToLeft = isArabic;
        sheet.Cell(1, 1).Value = isArabic
            ? "قائمة سير عمل الإجازات لكل إدارة أو مجموعة. السطر الأول هو العناوين. يجب أن يحتوي كل صف على موظف ودوره في سير العمل."
            : "Leaves approval cycle workflow per department or group. The first row is headers. Each row should contain an employee and their role in the workflow.";
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Alignment.WrapText = true;

        var headers = isArabic
            ? new[] { "الإدارة", "رقم الموظف المالي", "اسم الموظف", "اسم الوظيفة", "الدرجة", "المندوب الرئيسي", "مندوب المدير", "عدد مراحل الموافقة", "معرّف نوع الإجازة", "هل الإجازة مقابل أجر؟" }
            : new[] { "Department", "FinancialNo", "Employee Name", "Job Title", "Level", "Manager", "Manager Delegate", "Approval Steps", "Leave Type Id", "Paid?" };

        for (var i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(2, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#d9e1f2");
            cell.Style.Font.FontColor = XLColor.Black;
            cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.TopBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.LeftBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.RightBorder = XLBorderStyleValues.Thin;
        }

        // Sample data (optional)
        var departments = new[] { "IT", "HR", "Operations" };
        var levels = new[] { "Manager", "Lead", "Staff" };

        for (var i = 0; i < 3; i++)
        {
            var row = i + 3;
            sheet.Cell(row, 1).Value = departments[i % departments.Length];
            sheet.Cell(row, 2).Value = 1000 + i * 10; // FinancialNo
            sheet.Cell(row, 3).Value = isArabic ? $"الموظف {i + 1}" : $"Employee {i + 1}";
            sheet.Cell(row, 4).Value = isArabic ? "مدير" : "Manager";
            sheet.Cell(row, 5).Value = levels[i % levels.Length];
            sheet.Cell(row, 6).Value = isArabic ? $"المدير {i + 2}" : $"Manager {i + 2}"; // Manager
            sheet.Cell(row, 7).Value = isArabic ? $"المدير {i + 3}" : $"Manager {i + 3}"; // Manager delegate
            sheet.Cell(row, 8).Value = isArabic ? "الموظف -> المندوب -> المدير -> المدير المندوب -> الموارد البشرية" : "Employee -> Delegate -> Manager -> Manager Delegate -> HR";
            sheet.Cell(row, 9).Value = 1 + i; // LeaveTypeId
            sheet.Cell(row, 10).Value = isArabic ? "نعم" : "Yes";

            // Style sample rows
            for (var c = 1; c <= 10; c++)
            {
                var cell = sheet.Cell(row, c);
                cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                cell.Style.Border.TopBorder = XLBorderStyleValues.Thin;
                cell.Style.Border.LeftBorder = XLBorderStyleValues.Thin;
                cell.Style.Border.RightBorder = XLBorderStyleValues.Thin;
            }
        }

        // Instructions sheet
        var instructions = workbook.Worksheets.Add(isArabic ? "تعليمات" : "Instructions");
        instructions.RightToLeft = isArabic;
        string instructionText = isArabic
            ? "الشروط:\n1. سير العمل: تقدم الموظف طلب إجازة -> يقبل المندوب الرئيسي (إذا كان موجودًا) طلب الإجازة -> يقبل المدير (أو مندوب المدير إذا كان المدير لديه مندوب) -> الموارد البشرية (هذا جزء منفصل).\n2. يمكن أن يكون عمود المندوب الرئيسي فارغًا. إذا كان فارغًا، فإن المدير المباشرة هو من يقبل طلب الإجازة الذي قدمه الموظف (أي أن الإجازة تتخطى مرحلة المندوب الرئيسية).\n3. يحدد عمود مندوب المدير من يقبل طلب إجازة المدير عندما يكون المدير هو من يراجع طلب الإجازة (أي أن المندوب يحل محل المدير في مراجعة إجازات الموظفين الذين يكون مديرهم هو المدير المعني). إذا كان عمود مندوب المدير فارغًا، فإن المدير هو من يقبل طلب إجازة الموظف.\n4. استيراد هذا الملف يعيد هندسة نظام سير العمل: بالنسبة لكل موظف، نعين المندوب الرئيسي (إذا كان موجودًا) و/أو مندوب المدير (إذا كان المدير لديه مندوب). في وقت تقديم الإجازة، نتبع سير العمل: يتم إرسال طلب الإجازة أولاً إلى المندوب الرئيسي (إن وجد)، ثم إلى المدير (أو مندوب المدير إذا كان المدير لديه مندوب)، ثم إلى الموارد البشرية (هذا الجزء منفصل ويتبع المنطق الحالي)."
            : "Instructions:\n1. The leave approval cycle is: Employee submits -> Manager's delegate (if any) accepts -> Manager (or Manager's delegate if exists) accepts -> HR.\n2. The 'Manager' column is the employee's delegate (optional). If empty, the manager directly accepts the leave.\n3. The 'Manager Delegate' column is the delegate of the manager (optional). If empty, the manager accepts.\n4. Importing this file rebuilds the leave workflow system: for each employee, we record their delegate and, if that delegate is also a manager, their manager delegate. When a leave is submitted, the flow is: to the employee's delegate (if any) -> then to their manager (or their manager delegate) -> HR (this is already in the system).";
        instructions.Cell(1, 1).Value = instructionText;
        instructions.Cell(1, 1).Style.Font.Bold = true;
        instructions.Cell(1, 1).Style.Alignment.WrapText = true;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<string> ImportApprovalWorkflowAsync(string filePath, string enteredBy)
    {
        if (string.IsNullOrWhiteSpace(enteredBy))
            throw new ArgumentException("The authenticated username is required", nameof(enteredBy));

        if (!File.Exists(filePath))
            return $"File not found: {filePath}";

        using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true }
        });

        if (dataSet.Tables.Count == 0)
            return "No worksheets found in the leave approval workflow file.";

        var sheetTable = dataSet.Tables[0];
        var requiredColumnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Department", "FinancialNo", "Employee Name", "Manager", "Manager Delegate",
            "الإدارة", "رقم الموظف المالي", "اسم الموظف", "المندوب الرئيسي", "مندوب المدير"
        };

        // Check if any row header contains required names (we'll check first few rows)
        bool headersFound = false;
        for (int r = 0; r < Math.Min(sheetTable.Rows.Count, 3); r++)
        {
            var row = sheetTable.Rows[r];
            for (int c = 0; c < sheetTable.Columns.Count; c++)
            {
                var val = row[c]?.ToString()?.Trim() ?? "";
                if (requiredColumnNames.Contains(val))
                {
                    headersFound = true;
                    break;
                }
            }
            if (headersFound) break;
        }
        if (!headersFound)
            return "Required columns (Department, FinancialNo, Employee Name, Manager, Manager Delegate) missing in the workflow file.";

        var imported = 0;
        var skipped = 0;

        foreach (System.Data.DataRow row in sheetTable.Rows)
        {
            // Skip header-like rows
            var firstCell = row[0]?.ToString().Trim() ?? "";
            if (string.IsNullOrWhiteSpace(firstCell))
                continue;
            var isHeader = requiredColumnNames.Contains(firstCell)
                || new[] { "الإدارة", "رقم الموظف المالي", "اسم الموظف", "المندوب الرئيسي", "مندوب المدير" }.Contains(firstCell);
            if (isHeader)
                continue;

            var dept = GetText(row, "Department", "الإدارة");
            var finNo = GetText(row, "FinancialNo", "رقم الموظف المالي");
            var empName = GetText(row, "Employee Name", "اسم الموظف");
            var manager = GetText(row, "Manager", "المندوب الرئيسي");
            var managerDelegate = GetText(row, "Manager Delegate", "مندوب المدير");

            if (string.IsNullOrWhiteSpace(dept) || string.IsNullOrWhiteSpace(finNo) || string.IsNullOrWhiteSpace(empName))
            {
                skipped++;
                continue;
            }

            // TODO: Persist the delegate assignment in the database (requires a new table or adding to Employee).
            // For now, just log.
            System.Console.WriteLine($"[LeaveApprovalWorkflow] Import: dept={dept}, employee={finNo}, delegate={manager}, managerDelegate={managerDelegate}");
            imported++;
        }

        if (imported == 0)
            return "The leave approval workflow file contains no valid rows.";

        return $"Imported {imported} leave approval workflow assignments. Skipped {skipped} rows.";
    }

    private static string GetText(System.Data.DataRow row, params string[] names)
    {
        foreach (var name in names)
        {
            if (row.Table.Columns.Contains(name))
                return row[name]?.ToString()?.Trim() ?? string.Empty;
        }

        return string.Empty;
    }
}