using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace WpfClient.Services;

internal static class ReportPdfPrinter
{
    public static async Task<bool> PrintAsync(DownloadedReportPdf pdf, Func<bool> stillAllowed)
    {
        if (!stillAllowed()) return false;
        using var reader = await PdfPageReader.OpenAsync(pdf.Content);
        if (!stillAllowed()) return false;

        var dialog = new PrintDialog { UserPageRangeEnabled = true, MinPage = 1, MaxPage = (uint)reader.PageCount };
        if (dialog.PrintTicket is { } ticket)
            ticket.PageOrientation = reader.IsLandscape ? PageOrientation.Landscape : PageOrientation.Portrait;
        if (dialog.ShowDialog() != true || !stillAllowed()) return false;

        var capabilities = dialog.PrintQueue.GetPrintCapabilities(dialog.PrintTicket);
        var imageable = capabilities.PageImageableArea;
        var printable = imageable is null ? new Rect(0, 0, dialog.PrintableAreaWidth, dialog.PrintableAreaHeight)
            : new Rect(imageable.OriginWidth, imageable.OriginHeight, imageable.ExtentWidth, imageable.ExtentHeight);
        var paper = new Size(capabilities.OrientedPageMediaWidth ?? printable.Right,
            capabilities.OrientedPageMediaHeight ?? printable.Bottom);
        int first = dialog.PageRangeSelection == PageRangeSelection.UserPages ? Math.Clamp(dialog.PageRange.PageFrom, 1, reader.PageCount) : 1;
        int last = dialog.PageRangeSelection == PageRangeSelection.UserPages ? Math.Clamp(dialog.PageRange.PageTo, first, reader.PageCount) : reader.PageCount;

        dialog.PrintDocument(new PdfPaginator(reader, paper, printable, first - 1, last - first + 1), pdf.FileName);
        return true;
    }

    private sealed class PdfPaginator(PdfPageReader reader, Size paper, Rect printable, int first, int count) : DocumentPaginator
    {
        public override bool IsPageCountValid => true;
        public override int PageCount => count;
        public override Size PageSize { get; set; } = paper;
        public override IDocumentPaginatorSource? Source => null;

        public override DocumentPage GetPage(int pageNumber)
        {
            if (pageNumber < 0 || pageNumber >= count) return DocumentPage.Missing;

            uint width = (uint)Math.Clamp(printable.Width * 3, 1200, 2600);
            var image = Task.Run(() => reader.ReadAsync(first + pageNumber, width)).GetAwaiter().GetResult();
            double scale = Math.Min(printable.Width / image.PixelWidth, printable.Height / image.PixelHeight);
            double w = image.PixelWidth * scale;
            double h = image.PixelHeight * scale;
            var bounds = new Rect(printable.X + (printable.Width - w) / 2, printable.Y + (printable.Height - h) / 2, w, h);
            var visual = new DrawingVisual();
            using (var canvas = visual.RenderOpen()) canvas.DrawImage(image, bounds);
            return new DocumentPage(visual, PageSize, new Rect(PageSize), bounds);
        }
    }
}
