using Application.Dtos.Orders;

namespace Application.Common.Interfaces;

public interface IOrderEditingService
{
    Task<OrderDetailDto?> UpdateAsync(int id, UpdateOrderDto request, CancellationToken ct = default);
    Task<OrderDetailDto?> AddItemAsync(int id, CreateOrderItemDto request, CancellationToken ct = default);
    Task<OrderDetailDto?> RemoveItemAsync(int id, int itemId, CancellationToken ct = default);
    Task<OrderDetailDto?> ChangeStatusAsync(int id, ChangeOrderStatusDto request, int userId, CancellationToken ct = default);
}
