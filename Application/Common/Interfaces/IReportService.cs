using Application.Dtos.Reports;

namespace Application.Common.Interfaces;

public interface IReportService
{
    Task<OrderReportDto> GetOrdersAsync(DateTime? from, DateTime? to, CancellationToken ct = default, bool kyiv = false);
    Task<TodayOrdersReportDto> GetTodayOrdersAsync(DateTime? date, CancellationToken ct = default, bool kyiv = false);
    Task<IReadOnlyList<ProductSalesReportItemDto>> GetProductsAsync(DateTime? from, DateTime? to, CancellationToken ct = default, bool kyiv = false);
    Task<IReadOnlyList<LowStockProductReportItemDto>> GetLowStockAsync(CancellationToken ct = default);
    Task<ManagerSummaryReportDto> GetManagerSummaryAsync(DateTime? from, DateTime? to, CancellationToken ct = default, bool kyiv = false);
}

public interface IBotReportService
{
    Task<ReportOutput> GenerateAsync(ReportRequest request, CancellationToken ct = default);
}
