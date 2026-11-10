using System.IO;
using System.Windows;
using Microsoft.Win32;
using WpfClient.Models;

namespace WpfClient.Services;

internal static class ReportPdfFiles
{
    public static async Task<bool> SaveAsync(DownloadedReportPdf pdf, Window? owner, Func<bool> stillAllowed)
    {
        var dialog = new SaveFileDialog
        {
            Title = LocalizationManager.Instance["PdfSaveTitle"], Filter = "PDF (*.pdf)|*.pdf",
            FileName = pdf.FileName, AddExtension = true, DefaultExt = ".pdf"
        };
        if (!stillAllowed() || (owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner)) != true || !stillAllowed())
            return false;
        await File.WriteAllBytesAsync(dialog.FileName, pdf.Content);
        return true;
    }
}
