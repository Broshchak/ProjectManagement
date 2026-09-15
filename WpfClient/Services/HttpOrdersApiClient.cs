using System.Net.Http;
using System.Net.Http.Json;
using WpfClient.Models;

namespace WpfClient.Services;

public sealed class HttpOrdersApiClient(HttpClient httpClient) : IOrdersApiClient
{
    public async Task<IReadOnlyList<OrderListItem>> GetOrdersAsync(CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<List<OrderListItem>>("api/orders", cancellationToken)
            ?? [];
    }

    public async Task<IReadOnlyList<ProductListItem>> GetProductsAsync(CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<List<ProductListItem>>("api/products", cancellationToken)
            ?? [];
    }

    public async Task<ReportSummary> GetReportSummaryAsync(CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<ReportSummary>("api/reports/summary", cancellationToken)
            ?? new ReportSummary();
    }
}
