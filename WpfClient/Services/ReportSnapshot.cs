using WpfClient.Models;

namespace WpfClient.Services;

// The dates travel with the data: PDF headers must never use edited UI filters.
public sealed record ReportSnapshot(
    DateTime From, DateTime To, DateTime Date, ReportSummary Summary,
    int DateOrdersCount, decimal DateOrdersAmount,
    IReadOnlyList<TodayOrderReportItem> Orders,
    IReadOnlyList<ProductSalesReportItem> ProductSales,
    IReadOnlyList<LowStockProductReportItem> LowStock, string ManagerText)
{
    public static async Task<ReportSnapshot> LoadAsync(IOrdersApiClient api, DateTime from, DateTime to, DateTime date)
    {
        if (from.Date > to.Date)
            throw new ArgumentException(LocalizationManager.Instance["InvalidReportRange"]);
        var summary = api.GetOrdersReportAsync(from.Date, to.Date);
        var today = api.GetTodayOrdersReportAsync(date.Date);
        var products = api.GetProductSalesReportAsync(from.Date, to.Date);
        var stock = api.GetLowStockReportAsync();
        var manager = api.GetManagerSummaryReportAsync(from.Date, to.Date);
        await Task.WhenAll(summary, today, products, stock, manager);
        var day = await today;
        return new(from.Date, to.Date, day.Date, await summary, day.TotalOrders, day.TotalAmount,
            day.Orders, await products, await stock, await manager);
    }
}
