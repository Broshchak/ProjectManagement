using Application.Common.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Infrastructure.Reports;

public sealed class QuestReportPdfRenderer : IReportPdfRenderer
{
    private readonly SemaphoreSlim slots = new(2);
    private const string Ink = "#172A3D";
    private const string Blue = "#205587";
    private const string Muted = "#53697D";
    private const string Line = "#D5E0EB";
    private const string Tint = "#EDF4FA";

    static QuestReportPdfRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseSystemFonts = false;
    }

    public async Task<byte[]> RenderAsync(ReportDocument report, CancellationToken ct)
    {
        await slots.WaitAsync(ct);
        try
        {
            ct.ThrowIfCancellationRequested();
            var pdf = Document.Create(document => document.Page(page =>
            {
                page.Size(report.Landscape ? PageSizes.A4.Landscape() : PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(text => text.FontFamily("Lato").FontSize(10).FontColor(Ink));
                page.Header().Column(header =>
                {
                    header.Spacing(5);
                    header.Item().Text(T(report, "ЗВІТИ • ЗАМОВЛЕННЯ", "REPORTS • ORDERS")).Bold().FontSize(9).FontColor(Blue);
                    header.Item().Text(report.Title).Bold().FontSize(21);
                    string period = report.From is { } from && report.To is { } to
                        ? $"{T(report, "Період", "Period")}: {from:dd.MM.yyyy} - {to:dd.MM.yyyy}"
                        : T(report, "Поточні дані", "Current data");
                    header.Item().Text($"{period}    |    {T(report, "Сформовано", "Generated")}: {report.GeneratedAt:dd.MM.yyyy HH:mm} (Europe/Kyiv)")
                        .FontSize(9).FontColor(Muted);
                    header.Item().PaddingTop(7).LineHorizontal(1).LineColor(Line);
                });
                page.Content().PaddingVertical(14).Column(content =>
                {
                    content.Spacing(13);
                    if (report.Context is { } context)
                        content.Item().Text(context).FontSize(9).FontColor(Muted);
                    if (report.Summary.Count > 0)
                        content.Item().Row(row =>
                        {
                            row.Spacing(8);
                            foreach (var metric in report.Summary)
                                row.RelativeItem().Background(Tint).Padding(9).Column(tile =>
                                {
                                    tile.Item().Text(metric.Label).FontSize(9).FontColor(Muted);
                                    tile.Item().PaddingTop(2).Text(metric.Value).FontSize(14).Bold().FontColor(Blue);
                                });
                        });
                    if (report.Headers.Count > 0 && report.Groups.Count == 0)
                        content.Item().Background(Tint).Padding(12).Text(T(report, "За вибраними умовами даних немає.", "No rows match the selection."));
                    foreach (var group in report.Groups)
                        content.Item().EnsureSpace(80).Column(section =>
                        {
                            if (group.Label is { } label)
                                section.Item().PaddingBottom(6).Text(label).Bold().FontSize(12).FontColor(Blue);
                            section.Item().Element(container => Table(container, report.Headers, group.Rows));
                        });
                    if (report.Note is { } note)
                        content.Item().BorderLeft(2).BorderColor(Blue).PaddingLeft(10).Column(block =>
                        {
                            block.Item().Text(T(report, "Примітка", "Note")).Bold().FontColor(Blue);
                            block.Item().PaddingTop(3).Text(note);
                        });
                });
                page.Footer().BorderTop(1).BorderColor(Line).PaddingTop(7).AlignRight().Text(text =>
                {
                    text.DefaultTextStyle(s => s.FontColor(Muted).FontSize(9));
                    text.Span(T(report, "Сторінка ", "Page "));
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            })).GeneratePdf();
            ct.ThrowIfCancellationRequested();
            return pdf;
        }
        finally { slots.Release(); }
    }

    private static void Table(IContainer container, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows) =>
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                foreach (var _ in headers) columns.RelativeColumn();
            });
            table.Header(header =>
            {
                foreach (var text in headers)
                    header.Cell().Background(Blue).PaddingVertical(7).PaddingHorizontal(7).Text(text).Bold().FontColor(Colors.White);
            });
            for (int index = 0; index < rows.Count; index++)
            {
                string background = index % 2 == 0 ? Colors.White : "#F5F8FB";
                var cells = rows[index];
                table.Cell().ColumnSpan((uint)headers.Count).ShowEntire().Background(background)
                    .BorderBottom(0.5f).BorderColor(Line).Row(row =>
                    {
                        foreach (var text in cells)
                            row.RelativeItem().PaddingVertical(6).PaddingHorizontal(7).Text(text);
                    });
            }
        });

    private static string T(ReportDocument report, string uk, string en) => report.Language == "en" ? en : uk;
}
