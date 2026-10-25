using System.ComponentModel.DataAnnotations;
using System.Data;
using Application.Common.Interfaces;
using Application.Dtos.Orders;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public sealed class OrderConflictException(string message) : Exception(message);

public sealed class OrderEditingService(AppDbContext db, IOrderRepository repository, IOrderService orders)
{
    public Task<OrderDetailDto?> UpdateAsync(int id, UpdateOrderDto request, CancellationToken ct) =>
        Locked(id, async order => await Update(order, request, ct), ct);

    public Task<OrderDetailDto?> AddItemAsync(int id, CreateOrderItemDto request, CancellationToken ct) =>
        Locked(id, async order =>
        {
            var current = (await repository.GetDetailByIdAsync(id, ct))!;
            return await Update(order, new(current.Version, current.Comment,
                current.Items.Select(i => new UpdateOrderItemDto(i.Id, i.ProductId, i.Quantity))
                    .Append(new(0, request.ProductId, request.Quantity)).ToList()), ct);
        }, ct);

    public Task<OrderDetailDto?> RemoveItemAsync(int id, int itemId, CancellationToken ct) =>
        Locked(id, async order =>
        {
            var current = (await repository.GetDetailByIdAsync(id, ct))!;
            if (current.Items.All(i => i.Id != itemId)) return null;
            return await Update(order, new(current.Version, current.Comment,
                current.Items.Where(i => i.Id != itemId).Select(i => new UpdateOrderItemDto(i.Id, i.ProductId, i.Quantity)).ToList()), ct);
        }, ct);

    public Task<OrderDetailDto?> ChangeStatusAsync(int id, ChangeOrderStatusDto request, int userId, CancellationToken ct) =>
        Locked(id, _ => orders.ChangeOrderStatusAsync(id, request, userId, ct), ct);

    private async Task<OrderDetailDto?> Locked(int id, Func<OrderEntity, Task<OrderDetailDto?>> action, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        // All order mutations take the same parent lock, including legacy item/status routes.
        var order = await db.Set<OrderEntity>()
            .FromSqlInterpolated($"SELECT * FROM orders WHERE id = {id} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (order is null) return null;
        var result = await action(order);
        await transaction.CommitAsync(ct);
        return result;
    }

    private async Task<OrderDetailDto> Update(OrderEntity order, UpdateOrderDto request, CancellationToken ct)
    {
        var current = (await repository.GetDetailByIdAsync(order.Id, ct))!;
        if (!string.Equals(current.Version, request.ExpectedVersion, StringComparison.Ordinal))
            throw new OrderConflictException("Order has changed. Reload it before saving.");
        if (current.IsStatusFinal || current.StatusCode is not ("NewOrder" or "Registered"))
            throw new ValidationException("Only new and registered orders can be edited.");
        if (request.Items is null || request.Items.Count == 0 || request.Items.Any(i => i.Id < 0 || i.ProductId <= 0 || i.Quantity <= 0))
            throw new ValidationException("An order must contain at least one item with a positive quantity.");
        if (request.Items.GroupBy(i => i.ProductId).Any(g => g.Count() > 1) ||
            request.Items.Where(i => i.Id != 0).GroupBy(i => i.Id).Any(g => g.Count() > 1))
            throw new ValidationException("Order items must not repeat.");
        await db.Entry(order).Collection(o => o.Items).LoadAsync(ct);
        var existing = order.Items.ToDictionary(i => i.Id);
        foreach (var item in request.Items.Where(i => i.Id != 0))
            if (!existing.TryGetValue(item.Id, out var old) || old.ProductId != item.ProductId)
                throw new ValidationException("The item does not belong to this order or its product has changed.");
        int[] productIds = request.Items.Select(i => i.ProductId).ToArray();
        var products = await db.Set<ProductEntity>().Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var stock = await db.Set<ProductStockEntity>().Where(p => productIds.Contains(p.ProductId)).ToDictionaryAsync(p => p.ProductId, ct);
        var prices = await db.Set<ProductPriceEntity>().Where(p => productIds.Contains(p.ProductId)).ToDictionaryAsync(p => p.ProductId, ct);
        // Validate the entire draft before changing tracked entities.
        foreach (var item in request.Items)
        {
            if (!products.TryGetValue(item.ProductId, out var product) || !product.IsActive)
                throw new ValidationException($"Product {item.ProductId} is unavailable.");
            if (!stock.TryGetValue(item.ProductId, out var balance) || item.Quantity > balance.Quantity)
                throw new ValidationException($"Insufficient stock for product {item.ProductId}.");
            if (!current.Items.Any(i => i.ProductId == item.ProductId) &&
                (!prices.TryGetValue(item.ProductId, out var price) || price.Price <= 0))
                throw new ValidationException($"Price for product {item.ProductId} was not found.");
        }
        var byProduct = order.Items.ToDictionary(i => i.ProductId);
        foreach (var old in order.Items.Where(i => !productIds.Contains(i.ProductId)).ToList())
            db.Remove(old);
        foreach (var item in request.Items)
        {
            if (byProduct.TryGetValue(item.ProductId, out var old)) old.Quantity = item.Quantity;
            else db.Add(new OrderItemEntity { Order = order, ProductId = item.ProductId, Quantity = item.Quantity, UnitPrice = prices[item.ProductId].Price });
        }
        order.Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        // Touch the parent even for item-only updates so its timestamp/revision advances.
        db.Entry(order).Property(o => o.Comment).IsModified = true;
        await db.SaveChangesAsync(ct);
        return (await repository.GetDetailByIdAsync(order.Id, ct))!;
    }
}
