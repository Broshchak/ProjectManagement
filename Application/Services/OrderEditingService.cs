using System.ComponentModel.DataAnnotations;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Dtos.Orders;
using Domain.Entities;

namespace Application.Services;

public sealed class OrderEditingService(IOrderRepository repository, IProductRepository products,
    IUnitOfWork unitOfWork, IOrderService orders) : IOrderEditingService
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

    private Task<OrderDetailDto?> Locked(int id, Func<OrderEntity, Task<OrderDetailDto?>> action, CancellationToken ct) =>
        unitOfWork.InTransactionAsync(async token =>
        {
            var order = await repository.GetForUpdateAsync(id, token);
            return order is null ? null : await action(order);
        }, ct);

    private async Task<OrderDetailDto> Update(OrderEntity order, UpdateOrderDto request, CancellationToken ct)
    {
        var current = (await repository.GetDetailByIdAsync(order.Id, ct))!;
        if (!string.Equals(current.Version, request.ExpectedVersion, StringComparison.Ordinal))
            throw new DataConflictException("Order has changed. Reload it before saving.");
        if (current.IsStatusFinal || current.StatusCode is not ("NewOrder" or "Registered"))
            throw new ValidationException("Only new and registered orders can be edited.");
        if (request.Items is null || request.Items.Count == 0 || request.Items.Any(i => i.Id < 0 || i.ProductId <= 0 || i.Quantity <= 0))
            throw new ValidationException("An order must contain at least one item with a positive quantity.");
        if (request.Items.GroupBy(i => i.ProductId).Any(g => g.Count() > 1) ||
            request.Items.Where(i => i.Id != 0).GroupBy(i => i.Id).Any(g => g.Count() > 1))
            throw new ValidationException("Order items must not repeat.");
        var existing = order.Items.ToDictionary(i => i.Id);
        foreach (var item in request.Items.Where(i => i.Id != 0))
            if (!existing.TryGetValue(item.Id, out var old) || old.ProductId != item.ProductId)
                throw new ValidationException("The item does not belong to this order or its product has changed.");
        int[] productIds = request.Items.Select(i => i.ProductId).ToArray();
        var availability = (await products.GetAvailabilityAsync(productIds, ct)).ToDictionary(p => p.ProductId);
        // Validate the entire draft before changing tracked entities.
        foreach (var item in request.Items)
        {
            if (!availability.TryGetValue(item.ProductId, out var product) || !product.IsActive)
                throw new ValidationException($"Product {item.ProductId} is unavailable.");
            if (!product.Stock.HasValue || item.Quantity > product.Stock.Value)
                throw new ValidationException($"Insufficient stock for product {item.ProductId}.");
            if (!current.Items.Any(i => i.ProductId == item.ProductId) &&
                (!product.Price.HasValue || product.Price.Value <= 0))
                throw new ValidationException($"Price for product {item.ProductId} was not found.");
        }
        var byProduct = order.Items.ToDictionary(i => i.ProductId);
        foreach (var old in order.Items.Where(i => !productIds.Contains(i.ProductId)).ToList())
            repository.RemoveItem(old);
        foreach (var item in request.Items)
        {
            if (byProduct.TryGetValue(item.ProductId, out var old)) old.Quantity = item.Quantity;
            else await repository.AddItemAsync(new OrderItemEntity { Order = order, ProductId = item.ProductId, Quantity = item.Quantity, UnitPrice = availability[item.ProductId].Price!.Value }, ct);
        }
        order.Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        // Touch the parent even for item-only updates so its timestamp/revision advances.
        repository.Touch(order);
        await unitOfWork.SaveChangesAsync(ct);
        return (await repository.GetDetailByIdAsync(order.Id, ct))!;
    }
}
