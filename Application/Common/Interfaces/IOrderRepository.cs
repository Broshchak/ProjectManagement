using Application.Dtos.Orders;
using Domain.Entities;

namespace Application.Common.Interfaces
{
    public interface IOrderRepository
    {
        Task<OrderEntity?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<IEnumerable<OrderSummaryDto>> GetSummariesAsync(CancellationToken ct = default);
        Task<OrderDetailDto?> GetDetailByIdAsync(int id, CancellationToken ct = default);
        Task<OrderStatusEntity?> GetStatusByCodeAsync(string code, CancellationToken ct = default);
        Task<decimal?> GetProductPriceAsync(int productId, CancellationToken ct = default);
        Task<Dictionary<int, decimal>> GetProductPricesAsync(IEnumerable<int> productIds, CancellationToken ct = default);
        Task AddAsync(OrderEntity order, CancellationToken ct = default);
        Task AddStatusHistoryAsync(OrderStatusHistoryEntity history, CancellationToken ct = default);
        Task<OrderItemEntity?> GetItemByIdAsync(int orderId, int itemId, CancellationToken ct = default);
        Task AddItemAsync(OrderItemEntity item, CancellationToken ct = default);
        void RemoveItem(OrderItemEntity item);

    }

}
