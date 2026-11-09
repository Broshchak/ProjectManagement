using Application.Common.Interfaces;
using Application.Dtos.Reports;

namespace Application.Services;

public sealed class ReportService(IReportQueries queries) : IReportService
{
    private const int LowStockLimit = 5;

    public async Task<OrderReportDto> GetOrdersAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var orders = await queries.GetOrdersAsync(Start(from), End(to), ct);
        var products = await queries.GetProductSalesAsync(Start(from), End(to), ct);
        var topProduct = products.OrderByDescending(p => p.Quantity).FirstOrDefault();
        var lowStock = await queries.GetLowStockAsync(LowStockLimit, ct);
        return new OrderReportDto(orders.Count,
            orders.Count(o => o.StatusCode == "NewOrder"),
            orders.Count(o => o.StatusCode is "Registered" or "Granted"),
            orders.Count(o => o.StatusCode == "Shipped"),
            orders.Count(o => o.StatusCode == "Invoiced"),
            orders.Count(o => o.StatusCode == "Cancelled"),
            orders.Sum(o => o.TotalAmount), orders.Count == 0 ? 0 : orders.Average(o => o.TotalAmount),
            lowStock.Count, topProduct?.ProductName ?? "-", topProduct?.Quantity ?? 0);
    }

    public async Task<TodayOrdersReportDto> GetTodayOrdersAsync(DateTime? date, CancellationToken ct = default)
    {
        var day = Start(date ?? DateTime.UtcNow)!.Value;
        var orders = await queries.GetOrdersAsync(day, day.AddDays(1), ct);
        return new TodayOrdersReportDto(day, orders.Count, orders.Sum(o => o.TotalAmount),
            orders.OrderByDescending(o => o.CreatedAt).ToList());
    }

    public Task<IReadOnlyList<ProductSalesReportItemDto>> GetProductsAsync(DateTime? from, DateTime? to, CancellationToken ct = default) =>
        queries.GetProductSalesAsync(Start(from), End(to), ct);

    public Task<IReadOnlyList<LowStockProductReportItemDto>> GetLowStockAsync(CancellationToken ct = default) =>
        queries.GetLowStockAsync(LowStockLimit, ct);

    public async Task<ManagerSummaryReportDto> GetManagerSummaryAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var report = await GetOrdersAsync(from, to, ct);
        string period = from.HasValue && to.HasValue ? $"Період: {from.Value:dd.MM.yyyy} - {to.Value:dd.MM.yyyy}" :
            from.HasValue ? $"Період: з {from.Value:dd.MM.yyyy}" : to.HasValue ? $"Період: до {to.Value:dd.MM.yyyy}" : "Період: весь час";
        string text = $"Звіт для керівника\n{period}\n" +
            $"Замовлень: {report.TotalOrders}\nЗагальна сума: {report.TotalAmount:N2} грн\n" +
            $"Середній чек: {report.AverageOrderAmount:N2} грн\n" +
            $"Нові: {report.NewOrders}, в роботі: {report.OrdersInProgress}, відвантажені: {report.ShippedOrders}, рахунки: {report.InvoicedOrders}, скасовані: {report.CancelledOrders}\n" +
            $"Топ-товар: {(report.TopProductQuantity == 0 ? "немає даних" : $"{report.TopProductName} ({report.TopProductQuantity} од.)")}\n" +
            $"Товарів з низьким залишком: {report.LowStockProducts}";
        return new ManagerSummaryReportDto(DateTime.Now, text);
    }

    // Preserve the existing API's UTC-day semantics during the persistence refactor.
    private static DateTime? Start(DateTime? value) => value.HasValue ? DateTime.SpecifyKind(value.Value.Date, DateTimeKind.Utc) : null;
    private static DateTime? End(DateTime? value) => Start(value)?.AddDays(1);
}
