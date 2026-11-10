using Application.Dtos.Reports;

namespace Application.Common.Interfaces;

public interface IReportQueries
{
    // Bounds are UTC instants; the upper bound is exclusive.
    Task<IReadOnlyList<OrderReportItemDto>> GetOrdersAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken ct = default,
        int? rowLimit = null, string? statusCode = null, string? category = null);
    Task<IReadOnlyList<ProductSalesReportItemDto>> GetProductSalesAsync(DateTime? fromUtc, DateTime? toUtc,
        CancellationToken ct = default, int? rowLimit = null);
    Task<IReadOnlyList<LowStockProductReportItemDto>> GetLowStockAsync(int limit, CancellationToken ct = default, int? rowLimit = null);
}
