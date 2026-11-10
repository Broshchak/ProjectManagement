using System.Windows;
using WpfClient.Models;
using WpfClient.Services;
using WpfClient.ViewModels;

namespace WpfClient.Views;

public partial class ReportTemplateWindow : Window
{
    private readonly Func<bool> isCurrent;
    private bool closed;

    public ReportTemplateWindow(IReportPdfApiClient api, bool canEdit, DateTime from, DateTime to,
        IReadOnlyList<ReportChoice> statuses, Func<bool> isCurrent)
    {
        this.isCurrent = isCurrent;
        InitializeComponent();
        var vm = new ReportTemplateViewModel(api, canEdit, from, to, CanDeliver, DeliverAsync,
            () => MessageBox.Show(this, LocalizationManager.Instance["ReportDeleteConfirm"],
                LocalizationManager.Instance["ReportTemplates"], MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes);
        DataContext = vm;
        Loaded += async (_, _) => await vm.LoadAsync(statuses);
        Closed += (_, _) => closed = true;
    }

    private bool CanDeliver() => !closed && isCurrent();

    private async Task<bool> DeliverAsync(DownloadedReportPdf pdf, ReportPdfAction action)
    {
        if (action == ReportPdfAction.Save) return await ReportPdfFiles.SaveAsync(pdf, this, CanDeliver);
        if (action == ReportPdfAction.Print) return await ReportPdfPrinter.PrintAsync(pdf, CanDeliver);
        if (!CanDeliver()) return false;
        var window = new ReportPreviewWindow(pdf.Content) { Owner = this };
        window.ShowDialog();
        return true;
    }
}
