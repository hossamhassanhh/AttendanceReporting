using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AttendanceApp.Services;

public sealed class TabularReportExportService
{
    public byte[] GenerateExcel(
        string title,
        string? subtitle,
        string sheetName,
        IReadOnlyList<string> columns,
        IReadOnlyList<IReadOnlyList<object?>> rows,
        bool isArabic)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(SanitizeSheetName(sheetName));
        worksheet.RightToLeft = isArabic;

        worksheet.Cell(1, 1).Value = title;
        worksheet.Range(1, 1, 1, columns.Count).Merge();
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 16;
        worksheet.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("#1A2744");

        var headerRow = 3;
        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            worksheet.Cell(2, 1).Value = subtitle;
            worksheet.Range(2, 1, 2, columns.Count).Merge();
        }

        for (var column = 0; column < columns.Count; column++)
            worksheet.Cell(headerRow, column + 1).Value = columns[column];

        var header = worksheet.Range(headerRow, 1, headerRow, columns.Count);
        header.Style.Font.Bold = true;
        header.Style.Font.FontColor = XLColor.White;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#1A2744");
        header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var values = rows[rowIndex];
            for (var column = 0; column < columns.Count; column++)
                worksheet.Cell(headerRow + rowIndex + 1, column + 1).Value =
                    Convert.ToString(column < values.Count ? values[column] : null) ?? string.Empty;
        }

        var reportRange = worksheet.Range(headerRow, 1, headerRow + Math.Max(rows.Count, 1), columns.Count);
        reportRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        reportRange.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
        reportRange.Style.Border.OutsideBorderColor = XLColor.FromHtml("#CBD5E1");
        reportRange.Style.Border.InsideBorderColor = XLColor.FromHtml("#CBD5E1");
        reportRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        worksheet.SheetView.FreezeRows(headerRow);
        worksheet.Columns().AdjustToContents(10, 45);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] GeneratePdf(
        string title,
        string? subtitle,
        IReadOnlyList<string> columns,
        IReadOnlyList<IReadOnlyList<object?>> rows,
        bool isArabic)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.DefaultTextStyle(style => style.FontSize(8).FontColor("#1A2744"));

                page.Header().Column(header =>
                {
                    var heading = header.Item().Text(title).FontSize(16).Bold();
                    if (isArabic) heading.AlignRight(); else heading.AlignLeft();
                    if (!string.IsNullOrWhiteSpace(subtitle))
                    {
                        var detail = header.Item().PaddingTop(3).Text(subtitle).FontSize(9).FontColor("#64748B");
                        if (isArabic) detail.AlignRight(); else detail.AlignLeft();
                    }
                });

                var content = page.Content().PaddingTop(12);
                if (isArabic) content = content.ContentFromRightToLeft();
                content.Table(table =>
                {
                    table.ColumnsDefinition(definition =>
                    {
                        foreach (var _ in columns)
                            definition.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        foreach (var column in columns)
                        {
                            header.Cell()
                                .Background("#1A2744")
                                .Border(0.5f)
                                .BorderColor("#A8B7CA")
                                .Padding(5)
                                .Text(column)
                                .Bold()
                                .FontColor(Colors.White);
                        }
                    });

                    foreach (var row in rows)
                    {
                        for (var column = 0; column < columns.Count; column++)
                        {
                            table.Cell()
                                .BorderBottom(0.5f)
                                .BorderColor("#D7E0EA")
                                .Padding(4)
                                .Text(Convert.ToString(column < row.Count ? row[column] : null) ?? string.Empty);
                        }
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        });

        using var stream = new MemoryStream();
        document.GeneratePdf(stream);
        return stream.ToArray();
    }

    private static string SanitizeSheetName(string name)
    {
        var invalid = new[] { ':', '\\', '/', '?', '*', '[', ']' };
        foreach (var character in invalid)
            name = name.Replace(character, '-');
        return string.IsNullOrWhiteSpace(name) ? "Report" : name[..Math.Min(name.Length, 31)];
    }
}
