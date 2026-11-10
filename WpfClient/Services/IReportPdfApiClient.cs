using Orders.Reports;

namespace WpfClient.Services;

public interface IReportPdfApiClient
{
    Task<IReadOnlyList<ReportSource>> GetReportSourcesAsync();
    Task<IReadOnlyList<ReportTemplate>> GetReportTemplatesAsync();
    Task<ReportTemplate> SaveReportTemplateAsync(int? id, SaveReportTemplate request);
    Task DeleteReportTemplateAsync(int id, long version);
    Task<DownloadedReportPdf> GenerateReportPdfAsync(GenerateReportPdf request);
    Task<DownloadedReportPdf> PreviewReportPdfAsync(GenerateReportPdf request);
}

public sealed record DownloadedReportPdf(string FileName, byte[] Content);

public interface IOrderPdfApiClient
{
    Task<DownloadedReportPdf> GetOrderPdfAsync(int orderId, string language);
}
