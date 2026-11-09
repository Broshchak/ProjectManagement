using Application.Dtos.Reports;

namespace Application.Common.Interfaces;

public interface IReportQueries
{
    // Bounds are UTC instants; the upper bound is exclusive.
    Task<IReadOnlyList<OrderReportItemDto>> GetOrdersAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken ct = default);
    Task<IReadOnlyList<ProductSalesReportItemDto>> GetProductSalesAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken ct = default);
    Task<IReadOnlyList<LowStockProductReportItemDto>> GetLowStockAsync(int limit, CancellationToken ct = default);
}
