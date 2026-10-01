using Application.Common.Interfaces;
using Application.Dtos.Orders;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class OrderRepository(AppDbContext context) : IOrderRepository
    {
        public async Task<OrderEntity?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            return await context.Set<OrderEntity>()
                .FirstOrDefaultAsync(o => o.Id == id, ct);
        }

        public async Task<OrderStatusEntity?> GetStatusByCodeAsync(string code, CancellationToken ct = default)
        {
            return await context.Set<OrderStatusEntity>()
                .FirstOrDefaultAsync(s => s.Code == code, ct);
        }

        public async Task<decimal?> GetProductPriceAsync(int productId, CancellationToken ct = default)
        {
            var priceEntity = await context.Set<ProductPriceEntity>()
                .FirstOrDefaultAsync(p => p.ProductId == productId, ct);

            return priceEntity?.Price;
        }

        public async Task<IEnumerable<OrderSummaryDto>> GetSummariesAsync(CancellationToken ct = default)
        {
            return await (from o in context.Set<OrderEntity>().AsNoTracking()
                          join c in context.Set<CustomerEntity>().AsNoTracking() on o.CustomerId equals c.Id
                          join s in context.Set<OrderStatusEntity>().AsNoTracking() on o.StatusId equals s.Id
                          orderby o.CreatedAt descending
                          select new OrderSummaryDto(
                              o.Id,
                              o.OrderNumber,
                              s.Code,
                              s.Name,
                              s.IsFinal,
                              o.CustomerId,
                              c.FullName,
                              context.Set<OrderItemEntity>()
                                  .Where(i => i.OrderId == o.Id)
                                  .Sum(i => (decimal?)(i.Quantity * i.UnitPrice)) ?? 0m,
                              (from oi in context.Set<OrderItemEntity>()
                               join pp in context.Set<ProductPriceEntity>() on oi.ProductId equals pp.ProductId
                               where oi.OrderId == o.Id
                               select pp.CurrencyCode).FirstOrDefault() ?? "UAH",
                              o.Comment,
                              o.CreatedAt
                          )).ToListAsync(ct);
        }

        public async Task<OrderDetailDto?> GetDetailByIdAsync(int id, CancellationToken ct = default)
        {
            var orderData = await (from o in context.Set<OrderEntity>().AsNoTracking()
                                   join c in context.Set<CustomerEntity>().AsNoTracking() on o.CustomerId equals c.Id
                                   join s in context.Set<OrderStatusEntity>().AsNoTracking() on o.StatusId equals s.Id
                                   where o.Id == id
                                   select new
                                   {
                                       o.Id,
                                       o.OrderNumber,
                                       StatusCode = s.Code,
                                       StatusName = s.Name,
                                       s.IsFinal,
                                       o.CustomerId,
                                       CustomerFullName = c.FullName,
                                       CustomerPhone = c.Phone,
                                       CustomerEmail = c.Email,
                                       CustomerAddress = c.Address,
                                       o.CreatedByUserId,
                                       o.Comment,
                                       o.CreatedAt,
                                       o.UpdatedAt
                                   }).FirstOrDefaultAsync(ct);

            if (orderData == null) return null;

            var items = await (from oi in context.Set<OrderItemEntity>().AsNoTracking()
                               join p in context.Set<ProductEntity>().AsNoTracking() on oi.ProductId equals p.Id
                               join pp in context.Set<ProductPriceEntity>().AsNoTracking() on p.Id equals pp.ProductId into prices
                               from price in prices.DefaultIfEmpty()
                               where oi.OrderId == id
                               select new OrderItemDto(
                                   oi.Id,
                                   oi.ProductId,
                                   p.Name,
                                   oi.Quantity,
                                   oi.UnitPrice,
                                   price != null ? price.CurrencyCode : "UAH",
                                   oi.Quantity * oi.UnitPrice
                               )).ToListAsync(ct);

            var history = await (from h in context.Set<OrderStatusHistoryEntity>().AsNoTracking()
                                 join prevS in context.Set<OrderStatusEntity>().AsNoTracking() on h.PreviousStatusId equals prevS.Id into prevStatuses
                                 from prev in prevStatuses.DefaultIfEmpty()
                                 join newS in context.Set<OrderStatusEntity>().AsNoTracking() on h.NewStatusId equals newS.Id
                                 join u in context.Set<AppUserEntity>().AsNoTracking() on h.ChangedByUserId equals u.Id
                                 where h.OrderId == id
                                 orderby h.ChangedAt ascending
                                 select new OrderStatusHistoryDto(
                                     prev != null ? prev.Code : null,
                                     prev != null ? prev.Name : null,
                                     newS.Code,
                                     newS.Name,
                                     h.ChangedByUserId,
                                     u.FullName,
                                     h.ChangedAt,
                                     h.Comment
                                 )).ToListAsync(ct);

            var totalAmount = items.Sum(i => i.LineTotal);
            var currencyCode = items.FirstOrDefault()?.CurrencyCode ?? "UAH";

            return new OrderDetailDto(
                orderData.Id,
                orderData.OrderNumber,
                orderData.StatusCode,
                orderData.StatusName,
                orderData.IsFinal,
                orderData.CustomerId,
                orderData.CustomerFullName,
                orderData.CustomerPhone,
                orderData.CustomerEmail,
                orderData.CustomerAddress,
                orderData.CreatedByUserId,
                totalAmount,
                currencyCode,
                orderData.Comment,
                orderData.CreatedAt,
                orderData.UpdatedAt,
                items,
                history
            );
        }

        public async Task<Dictionary<int, decimal>> GetProductPricesAsync(IEnumerable<int> productIds, CancellationToken ct = default)
        {
            return await context.Set<ProductPriceEntity>()
                .AsNoTracking()
                .Where(p => productIds.Contains(p.ProductId))
                .ToDictionaryAsync(p => p.ProductId, p => p.Price, ct);
        }

        public async Task AddAsync(OrderEntity order, CancellationToken ct = default)
        {
            await context.Set<OrderEntity>().AddAsync(order, ct);
        }

        public async Task AddStatusHistoryAsync(OrderStatusHistoryEntity history, CancellationToken ct = default)
        {
            await context.Set<OrderStatusHistoryEntity>().AddAsync(history, ct);
        }

        public async Task<OrderItemEntity?> GetItemByIdAsync(int orderId, int itemId, CancellationToken ct = default)
        {
            return await context.Set<OrderItemEntity>()
                .FirstOrDefaultAsync(oi => oi.OrderId == orderId && oi.Id == itemId, ct);
        }

        public async Task AddItemAsync(OrderItemEntity item, CancellationToken ct = default)
        {
            await context.Set<OrderItemEntity>().AddAsync(item, ct);
        }

        public void RemoveItem(OrderItemEntity item)
        {
            context.Set<OrderItemEntity>().Remove(item);
        }
    }
}
