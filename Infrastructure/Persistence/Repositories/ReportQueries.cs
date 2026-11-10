using Application.Common.Interfaces;
using Application.Dtos.Reports;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public sealed class ReportQueries(AppDbContext db) : IReportQueries
{
    public async Task<IReadOnlyList<OrderReportItemDto>> GetOrdersAsync(DateTime? fromUtc, DateTime? toUtc,
        CancellationToken ct = default, int? rowLimit = null, string? statusCode = null, string? category = null) =>
        await Limit(from order in Orders(fromUtc, toUtc)
            join customer in db.Set<CustomerEntity>().AsNoTracking() on order.CustomerId equals customer.Id
            join status in db.Set<OrderStatusEntity>().AsNoTracking() on order.StatusId equals status.Id
            where (statusCode == null || status.Code == statusCode) && (category == null || status.Category == category)
            orderby order.CreatedAt descending, order.Id descending
            select new OrderReportItemDto(order.Id, order.OrderNumber, customer.FullName, status.Code, status.Name,
                db.Set<OrderItemEntity>().Where(item => item.OrderId == order.Id)
                    .Sum(item => (decimal?)(item.Quantity * item.UnitPrice)) ?? 0m,
                order.CreatedAt, status.Category), rowLimit).ToListAsync(ct);

    public async Task<IReadOnlyList<ProductSalesReportItemDto>> GetProductSalesAsync(DateTime? fromUtc, DateTime? toUtc,
        CancellationToken ct = default, int? rowLimit = null)
    {
        var rows = from item in db.Set<OrderItemEntity>().AsNoTracking()
            join order in Orders(fromUtc, toUtc) on item.OrderId equals order.Id
            join product in db.Set<ProductEntity>().AsNoTracking() on item.ProductId equals product.Id
            join category in db.Set<ProductCategoryEntity>().AsNoTracking() on product.CategoryId equals category.Id
            group item by new { product.Id, product.Name, CategoryName = category.Name } into grouped
            select new
            {
                grouped.Key.Id, grouped.Key.Name, grouped.Key.CategoryName,
                Quantity = grouped.Sum(item => item.Quantity),
                TotalAmount = grouped.Sum(item => item.Quantity * item.UnitPrice)
            };
        return await Limit(rows.OrderByDescending(row => row.TotalAmount).ThenBy(row => row.Id)
            .Select(row => new ProductSalesReportItemDto(row.Id, row.Name, row.CategoryName, row.Quantity, row.TotalAmount)), rowLimit)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<LowStockProductReportItemDto>> GetLowStockAsync(int limit, CancellationToken ct = default, int? rowLimit = null) =>
        await Limit(from product in db.Set<ProductEntity>().AsNoTracking()
            join category in db.Set<ProductCategoryEntity>().AsNoTracking() on product.CategoryId equals category.Id
            join stock in db.Set<ProductStockEntity>().AsNoTracking() on product.Id equals stock.ProductId
            join price in db.Set<ProductPriceEntity>().AsNoTracking() on product.Id equals price.ProductId into prices
            from price in prices.DefaultIfEmpty()
            where product.IsActive && stock.Quantity <= limit
            orderby stock.Quantity, product.Name, product.Id
            select new LowStockProductReportItemDto(product.Id, product.Name, category.Name,
                stock.Quantity, price == null ? 0m : price.Price), rowLimit).ToListAsync(ct);

    private static IQueryable<T> Limit<T>(IQueryable<T> query, int? count) => count is int rows ? query.Take(rows) : query;

    private IQueryable<OrderEntity> Orders(DateTime? fromUtc, DateTime? toUtc)
    {
        var orders = db.Set<OrderEntity>().AsNoTracking();
        if (fromUtc.HasValue) orders = orders.Where(o => o.CreatedAt >= fromUtc.Value);
        if (toUtc.HasValue) orders = orders.Where(o => o.CreatedAt < toUtc.Value);
        return orders;
    }
}
