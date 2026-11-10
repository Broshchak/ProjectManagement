using System.Windows;
using System.Windows.Input;
using Orders.Reports;
using WpfClient.Models;
using WpfClient.Services;
using WpfClient.Views;

namespace WpfClient.ViewModels;

public sealed partial class MainViewModel
{
    private ReportTemplateWindow? _reportTemplateWindow;
    private bool _reportPdfBusy;
    private bool _orderPrintBusy;
    private string? _reportPdfMessage;

    public ICommand OpenReportTemplatesCommand { get; }
    public ICommand PrintReportCommand { get; }
    public ICommand PrintOrderCommand { get; }
    public bool CanUseReportPdf => CanViewReports && _apiClient is IReportPdfApiClient;
    public bool HasOrderPdfSupport => _apiClient is IOrderPdfApiClient;
    public string? ReportPdfMessage
    {
        get => _reportPdfMessage;
        private set
        {
            _reportPdfMessage = value;
            OnPropertyChanged();
        }
    }

    private async Task OpenReportTemplatesAsync()
    {
        if (!CanUseReportPdf || _reportPdfBusy || _apiClient is not IReportPdfApiClient api) return;
        int session = _sessionVersion;
        _reportPdfBusy = true;
        ReportPdfMessage = null;
        CommandManager.InvalidateRequerySuggested();
        try
        {
            IReadOnlyList<ReportChoice> statuses = [];
            if (Can("orders.read") && _apiClient is IWorkflowApiClient workflow)
            {
                var catalog = await workflow.GetWorkflowAsync();
                statuses = catalog.States.Select(state => new ReportChoice(state.Code, OrderStatusNames.ToText(state.Code, state.Name))).ToArray();
            }

            bool IsCurrent() => session == _sessionVersion && IsAuthenticated && CanUseReportPdf;
            if (!IsCurrent()) return;
            _reportTemplateWindow = new ReportTemplateWindow(api, Can("reports.templates.manage"), ReportDateFrom, ReportDateTo, statuses, IsCurrent)
            {
                Owner = Application.Current?.MainWindow
            };
            _reportTemplateWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            if (session == _sessionVersion) ReportPdfMessage = ex.Message;
        }
        finally
        {
            _reportTemplateWindow = null;
            _reportPdfBusy = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private Task ExportReportsToPdfAsync() => OutputReportAsync(print: false);

    private async Task OutputReportAsync(bool print)
    {
        if (!CanUseReportPdf || _reportPdfBusy || _apiClient is not IReportPdfApiClient api) return;
        var from = DateOnly.FromDateTime(ReportDateFrom);
        var to = DateOnly.FromDateTime(ReportDateTo);
        var day = DateOnly.FromDateTime(TodayReportDate);
        string language = T.LanguageCode;
        var request = SelectedReportTabIndex switch
        {
            1 => new GenerateReportPdf(Preset: "orders", From: day, To: day, Language: language),
            2 => new GenerateReportPdf(Preset: "products", From: from, To: to, Language: language),
            3 => new GenerateReportPdf(Preset: "low-stock", Language: language),
            _ => new GenerateReportPdf(Preset: "summary", From: from, To: to, Language: language)
        };
        if (request.From > request.To)
        {
            ReportPdfMessage = T["InvalidReportRange"];
            return;
        }

        int session = _sessionVersion;
        bool IsCurrent() => session == _sessionVersion && IsAuthenticated && CanUseReportPdf;
        _reportPdfBusy = true;
        ReportPdfMessage = T["ReportGenerating"];
        CommandManager.InvalidateRequerySuggested();
        try
        {
            var pdf = await api.GenerateReportPdfAsync(request);
            if (!IsCurrent()) return;
            bool delivered = print ? await ReportPdfPrinter.PrintAsync(pdf, IsCurrent)
                : await ReportPdfFiles.SaveAsync(pdf, Application.Current?.MainWindow, IsCurrent);
            if (IsCurrent()) ReportPdfMessage = delivered ? T[print ? "PdfPrinted" : "PdfSaved"] : null;
        }
        catch (Exception ex)
        {
            if (IsCurrent()) ReportPdfMessage = T[print ? "PdfPrintFailed" : "PdfSaveFailed"] + " " + ex.Message;
        }
        finally
        {
            _reportPdfBusy = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private bool CanPrintSelectedOrder() =>
        HasOrderPdfSupport && IsAuthenticated && Can("orders.read", true) && !IsOrderDetailsLoading &&
        !_orderPrintBusy && SelectedOrderDetails is not null;

    private async Task PrintSelectedOrderAsync()
    {
        if (!CanPrintSelectedOrder() || SelectedOrderDetails is not { } order || _apiClient is not IOrderPdfApiClient api)
            return;

        int session = _sessionVersion;
        int selection = _detailVersion;
        bool IsCurrent() => session == _sessionVersion && selection == _detailVersion &&
            IsAuthenticated && Can("orders.read", true) && SelectedOrderDetails?.Id == order.Id;
        _orderPrintBusy = true;
        ApiErrorMessage = null;
        CommandManager.InvalidateRequerySuggested();
        try
        {
            var pdf = await api.GetOrderPdfAsync(order.Id, T.LanguageCode);
            await ReportPdfPrinter.PrintAsync(pdf, IsCurrent);
        }
        catch (Exception ex)
        {
            if (IsCurrent()) ApiErrorMessage = T["PdfPrintFailed"] + " " + ex.Message;
        }
        finally
        {
            _orderPrintBusy = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
