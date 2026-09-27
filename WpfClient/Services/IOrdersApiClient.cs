using WpfClient.Models;

namespace WpfClient.Services;

public interface IOrdersApiClient
{
    Task<IReadOnlyList<OrderListItem>> GetOrdersAsync(CancellationToken cancellationToken = default);

    Task<OrderDetail?> GetOrderDetailsAsync(string orderNumber, CancellationToken cancellationToken = default);

    Task ChangeOrderStatusAsync(string orderNumber, string action, string changedBy, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductListItem>> GetProductsAsync(CancellationToken cancellationToken = default);

    Task<ReportSummary> GetReportSummaryAsync(CancellationToken cancellationToken = default);
}
