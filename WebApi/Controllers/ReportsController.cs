using Application.Dtos.Reports;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace WebApi.Controllers;

[Route("api/reports")]
[ApiController]
public class ReportsController(AppDbContext context) : ControllerBase
{
    private const int LowStockLimit = 5;

    [HttpGet("orders")]
    public async Task<ActionResult<OrderReportDto>> GetOrdersReport([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null, CancellationToken ct = default)
    {
        var ordersQuery = ApplyDateRange(GetOrdersQuery(), from, to);
        var orders = await ordersQuery.ToListAsync(ct);

        var topProduct = await GetProductSalesQuery(from, to)
            .OrderByDescending(item => item.Quantity)
            .FirstOrDefaultAsync(ct);

        int lowStockProducts = await GetLowStockQuery().CountAsync(ct);

        return new OrderReportDto(
            orders.Count,
            orders.Count(order => order.StatusCode == "NewOrder"),
            orders.Count(order => order.StatusCode is "Registered" or "Granted"),
            orders.Count(order => order.StatusCode == "Shipped"),
            orders.Count(order => order.StatusCode == "Invoiced"),
            orders.Count(order => order.StatusCode == "Cancelled"),
            orders.Sum(order => order.TotalAmount),
            orders.Count == 0 ? 0 : orders.Average(order => order.TotalAmount),
            lowStockProducts,
            topProduct?.ProductName ?? "-",
            topProduct?.Quantity ?? 0);
    }

    [HttpGet("orders/today")]
    public async Task<ActionResult<TodayOrdersReportDto>> GetTodayOrdersReport([FromQuery] DateTime? date = null, CancellationToken ct = default)
    {
        DateTime reportDate = ToUtcDate(date ?? DateTime.UtcNow);
        DateTime nextDate = reportDate.AddDays(1);

        var orders = await GetOrdersQuery()
            .Where(order => order.CreatedAt >= reportDate && order.CreatedAt < nextDate)
            .OrderByDescending(order => order.CreatedAt)
            .ToListAsync(ct);

        return new TodayOrdersReportDto(
            reportDate,
            orders.Count,
            orders.Sum(order => order.TotalAmount),
            orders);
    }

    [HttpGet("products")]
    public async Task<ActionResult<IReadOnlyCollection<ProductSalesReportItemDto>>> GetProductsReport([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null, CancellationToken ct = default)
    {
        var products = await GetProductSalesQuery(from, to)
            .OrderByDescending(item => item.TotalAmount)
            .ToListAsync(ct);

        return products;
    }

    [HttpGet("low-stock")]
    public async Task<ActionResult<IReadOnlyCollection<LowStockProductReportItemDto>>> GetLowStockReport(CancellationToken ct = default)
    {
        var products = await GetLowStockQuery()
            .OrderBy(item => item.Quantity)
            .ThenBy(item => item.ProductName)
            .ToListAsync(ct);

        return products;
    }

    [HttpGet("manager-summary")]
    public async Task<ActionResult<ManagerSummaryReportDto>> GetManagerSummaryReport([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null, CancellationToken ct = default)
    {
        var ordersReport = await GetOrdersReport(from, to, ct);
        var report = ordersReport.Value!;
        string periodText = GetPeriodText(from, to);

        string text =
            $"Звіт для керівника\n" +
            $"{periodText}\n" +
            $"Замовлень: {report.TotalOrders}\n" +
            $"Загальна сума: {report.TotalAmount:N2} грн\n" +
            $"Середній чек: {report.AverageOrderAmount:N2} грн\n" +
            $"Нові: {report.NewOrders}, в роботі: {report.OrdersInProgress}, відвантажені: {report.ShippedOrders}, рахунки: {report.InvoicedOrders}, скасовані: {report.CancelledOrders}\n" +
            $"Топ-товар: {(report.TopProductQuantity == 0 ? "немає даних" : $"{report.TopProductName} ({report.TopProductQuantity} од.)")}\n" +
            $"Товарів з низьким залишком: {report.LowStockProducts}";

        return new ManagerSummaryReportDto(DateTime.Now, text);
    }

    private IQueryable<OrderReportItemDto> GetOrdersQuery()
    {
        return from o in context.Set<OrderEntity>().AsNoTracking()
            join c in context.Set<CustomerEntity>().AsNoTracking() on o.CustomerId equals c.Id
            join s in context.Set<OrderStatusEntity>().AsNoTracking() on o.StatusId equals s.Id
            select new OrderReportItemDto(
                o.Id,
                o.OrderNumber,
                c.FullName,
                s.Code,
                s.Name,
                context.Set<OrderItemEntity>()
                    .Where(item => item.OrderId == o.Id)
                    .Sum(item => (decimal?)(item.Quantity * item.UnitPrice)) ?? 0m,
                o.CreatedAt);
    }

    private IQueryable<ProductSalesReportItemDto> GetProductSalesQuery(DateTime? from = null, DateTime? to = null)
    {
        DateTime? dateFrom = from.HasValue ? ToUtcDate(from.Value) : null;
        DateTime? dateTo = to.HasValue ? ToUtcDate(to.Value).AddDays(1) : null;

        var query = from item in context.Set<OrderItemEntity>().AsNoTracking()
            join order in context.Set<OrderEntity>().AsNoTracking() on item.OrderId equals order.Id
            join product in context.Set<ProductEntity>().AsNoTracking() on item.ProductId equals product.Id
            join category in context.Set<ProductCategoryEntity>().AsNoTracking() on product.CategoryId equals category.Id
            select new
            {
                item.Quantity,
                item.UnitPrice,
                order.CreatedAt,
                product.Id,
                product.Name,
                CategoryName = category.Name
            };

        if (dateFrom.HasValue)
        {
            query = query.Where(row => row.CreatedAt >= dateFrom.Value);
        }

        if (dateTo.HasValue)
        {
            query = query.Where(row => row.CreatedAt < dateTo.Value);
        }

        return from row in query
            group row by new { row.Id, row.Name, row.CategoryName } into grouped
            select new ProductSalesReportItemDto(
                grouped.Key.Id,
                grouped.Key.Name,
                grouped.Key.CategoryName,
                grouped.Sum(item => item.Quantity),
                grouped.Sum(item => item.Quantity * item.UnitPrice));
    }

    private static IQueryable<OrderReportItemDto> ApplyDateRange(IQueryable<OrderReportItemDto> query, DateTime? from, DateTime? to)
    {
        if (from.HasValue)
        {
            DateTime dateFrom = ToUtcDate(from.Value);
            query = query.Where(order => order.CreatedAt >= dateFrom);
        }

        if (to.HasValue)
        {
            DateTime dateTo = ToUtcDate(to.Value).AddDays(1);
            query = query.Where(order => order.CreatedAt < dateTo);
        }

        return query;
    }

    private static DateTime ToUtcDate(DateTime value)
    {
        return DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);
    }

    private static string GetPeriodText(DateTime? from, DateTime? to)
    {
        if (from.HasValue && to.HasValue)
        {
            return $"Період: {from.Value:dd.MM.yyyy} - {to.Value:dd.MM.yyyy}";
        }

        if (from.HasValue)
        {
            return $"Період: з {from.Value:dd.MM.yyyy}";
        }

        if (to.HasValue)
        {
            return $"Період: до {to.Value:dd.MM.yyyy}";
        }

        return "Період: весь час";
    }

    private IQueryable<LowStockProductReportItemDto> GetLowStockQuery()
    {
        return from product in context.Set<ProductEntity>().AsNoTracking()
            join category in context.Set<ProductCategoryEntity>().AsNoTracking() on product.CategoryId equals category.Id
            join stock in context.Set<ProductStockEntity>().AsNoTracking() on product.Id equals stock.ProductId
            join price in context.Set<ProductPriceEntity>().AsNoTracking() on product.Id equals price.ProductId into prices
            from productPrice in prices.DefaultIfEmpty()
            where product.IsActive && stock.Quantity <= LowStockLimit
            select new LowStockProductReportItemDto(
                product.Id,
                product.Name,
                category.Name,
                stock.Quantity,
                productPrice != null ? productPrice.Price : 0m);
    }
}
