using Application.Dtos.Orders;

namespace Application.Common.Interfaces
{
    public interface IOrderService
    {
        Task<IEnumerable<OrderSummaryDto>> GetOrdersAsync(CancellationToken ct = default);
        Task<OrderDetailDto?> GetOrderByIdAsync(int id, CancellationToken ct = default);
        Task<OrderDetailDto> CreateOrderAsync(CreateOrderDto dto, int currentUserId, CancellationToken ct = default);
        Task<bool> AddOrderItemAsync(int orderId, CreateOrderItemDto dto, CancellationToken ct = default);
        Task<bool> RemoveOrderItemAsync(int orderId, int itemId, CancellationToken ct = default);
    }
}
