using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using Orders.Reports;

namespace WpfClient.Services;

public sealed partial class HttpOrdersApiClient : IReportPdfApiClient, IOrderPdfApiClient
{
    public async Task<IReadOnlyList<ReportSource>> GetReportSourcesAsync() =>
        await GetJsonAsync<ReportSource[]>("api/report-templates/catalog", default) ?? [];

    public async Task<IReadOnlyList<ReportTemplate>> GetReportTemplatesAsync() =>
        await GetJsonAsync<ReportTemplate[]>("api/report-templates", default) ?? [];

    public async Task<ReportTemplate> SaveReportTemplateAsync(int? id, SaveReportTemplate request)
    {
        using var response = await SendAsync(id.HasValue ? HttpMethod.Put : HttpMethod.Post,
            id.HasValue ? $"api/report-templates/{id}" : "api/report-templates", JsonContent.Create(request), default);
        return await response.Content.ReadFromJsonAsync<ReportTemplate>() ?? throw new InvalidDataException("Report template response is empty.");
    }

    public async Task DeleteReportTemplateAsync(int id, long version)
    {
        using var response = await SendAsync(HttpMethod.Delete,
            $"api/report-templates/{id}?expectedVersion={version.ToString(CultureInfo.InvariantCulture)}", null, default);
    }

    public Task<DownloadedReportPdf> GenerateReportPdfAsync(GenerateReportPdf request) =>
        DownloadReportPdfAsync("api/reports/pdf", request);

    public Task<DownloadedReportPdf> PreviewReportPdfAsync(GenerateReportPdf request) =>
        DownloadReportPdfAsync("api/reports/pdf/preview", request);

    public async Task<DownloadedReportPdf> GetOrderPdfAsync(int orderId, string language)
    {
        using var response = await SendAsync(HttpMethod.Get, $"api/orders/{orderId}/pdf?language={Uri.EscapeDataString(language)}", null, default);
        return await ReadPdfAsync(response);
    }

    private async Task<DownloadedReportPdf> DownloadReportPdfAsync(string path, GenerateReportPdf request)
    {
        using var response = await SendAsync(HttpMethod.Post, path, JsonContent.Create(request), default);
        return await ReadPdfAsync(response);
    }

    private static async Task<DownloadedReportPdf> ReadPdfAsync(HttpResponseMessage response)
    {
        if (response.Content.Headers.ContentType?.MediaType != "application/pdf")
            throw new InvalidDataException("The report endpoint did not return a PDF.");
        byte[] bytes = await response.Content.ReadAsByteArrayAsync();
        if (!bytes.AsSpan().StartsWith("%PDF-"u8))
            throw new InvalidDataException("The report endpoint returned an invalid PDF.");

        var disposition = response.Content.Headers.ContentDisposition;
        var name = Path.GetFileName((disposition?.FileNameStar ?? disposition?.FileName)?.Trim('"'));
        if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            !name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            name = "report.pdf";
        return new(name, bytes);
    }
}
