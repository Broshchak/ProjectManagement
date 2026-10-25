using Application.Dtos.Reports;

namespace Application.Common.Interfaces;

public interface IReportService
{
    Task<OrderReportDto> GetOrdersAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<TodayOrdersReportDto> GetTodayOrdersAsync(DateTime? date, CancellationToken ct = default);
    Task<IReadOnlyList<ProductSalesReportItemDto>> GetProductsAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<IReadOnlyList<LowStockProductReportItemDto>> GetLowStockAsync(CancellationToken ct = default);
    Task<ManagerSummaryReportDto> GetManagerSummaryAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
}
