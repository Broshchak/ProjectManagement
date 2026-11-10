using Application.Common.Interfaces;
using Application.Dtos.Reports;

namespace Application.Services;

public sealed class ReportService(IReportQueries queries) : IReportService
{
    private const int LowStockLimit = 5;

    public async Task<OrderReportDto> GetOrdersAsync(DateTime? from, DateTime? to, CancellationToken ct = default, bool kyiv = false)
    {
        var start = ReportCalendar.Start(from, kyiv);
        var end = ReportCalendar.Start(to?.AddDays(1), kyiv);
        var orders = await queries.GetOrdersAsync(start, end, ct);
        var products = await queries.GetProductSalesAsync(start, end, ct);
        var topProduct = products.OrderByDescending(p => p.Quantity).FirstOrDefault();
        var lowStock = await queries.GetLowStockAsync(LowStockLimit, ct);
        return new OrderReportDto(orders.Count,
            orders.Count(o => o.StatusCategory == "New"),
            orders.Count(o => o.StatusCategory == "InProgress"),
            orders.Count(o => o.StatusCategory == "Shipped"),
            orders.Count(o => o.StatusCategory == "Completed"),
            orders.Count(o => o.StatusCategory == "Cancelled"),
            orders.Sum(o => o.TotalAmount), orders.Count == 0 ? 0 : orders.Average(o => o.TotalAmount),
            lowStock.Count, topProduct?.ProductName ?? "-", topProduct?.Quantity ?? 0);
    }

    public async Task<TodayOrdersReportDto> GetTodayOrdersAsync(DateTime? date, CancellationToken ct = default, bool kyiv = false)
    {
        var day = DateTime.SpecifyKind((date ?? (kyiv ? ReportCalendar.InKyiv(DateTime.UtcNow) : DateTime.UtcNow)).Date, DateTimeKind.Utc);
        var orders = await queries.GetOrdersAsync(ReportCalendar.Start(day, kyiv), ReportCalendar.Start(day.AddDays(1), kyiv), ct);
        return new TodayOrdersReportDto(day, orders.Count, orders.Sum(o => o.TotalAmount),
            orders.OrderByDescending(o => o.CreatedAt).ToList());
    }

    public Task<IReadOnlyList<ProductSalesReportItemDto>> GetProductsAsync(DateTime? from, DateTime? to,
        CancellationToken ct = default, bool kyiv = false) =>
        queries.GetProductSalesAsync(ReportCalendar.Start(from, kyiv), ReportCalendar.Start(to?.AddDays(1), kyiv), ct);

    public Task<IReadOnlyList<LowStockProductReportItemDto>> GetLowStockAsync(CancellationToken ct = default) =>
        queries.GetLowStockAsync(LowStockLimit, ct);

    public async Task<ManagerSummaryReportDto> GetManagerSummaryAsync(DateTime? from, DateTime? to,
        CancellationToken ct = default, bool kyiv = false)
    {
        var report = await GetOrdersAsync(from, to, ct, kyiv);
        string period = from.HasValue && to.HasValue ? $"Період: {from.Value:dd.MM.yyyy} - {to.Value:dd.MM.yyyy}" :
            from.HasValue ? $"Період: з {from.Value:dd.MM.yyyy}" : to.HasValue ? $"Період: до {to.Value:dd.MM.yyyy}" : "Період: весь час";
        string text = $"Звіт для керівника\n{period}\n" +
            $"Замовлень: {report.TotalOrders}\nЗагальна сума: {report.TotalAmount:N2} грн\n" +
            $"Середній чек: {report.AverageOrderAmount:N2} грн\n" +
            $"Нові: {report.NewOrders}, в роботі: {report.OrdersInProgress}, відвантажені: {report.ShippedOrders}, завершені: {report.InvoicedOrders}, скасовані: {report.CancelledOrders}\n" +
            $"Топ-товар: {(report.TopProductQuantity == 0 ? "немає даних" : $"{report.TopProductName} ({report.TopProductQuantity} од.)")}\n" +
            $"Товарів з низьким залишком: {report.LowStockProducts}";
        return new ManagerSummaryReportDto(DateTime.Now, text);
    }

}
