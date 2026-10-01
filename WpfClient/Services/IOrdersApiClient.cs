using WpfClient.Models;

namespace WpfClient.Services;

public interface IOrdersApiClient
{
    Task<IReadOnlyList<OrderListItem>> GetOrdersAsync(CancellationToken cancellationToken = default);

    Task<OrderDetail?> GetOrderDetailsAsync(int orderId, CancellationToken cancellationToken = default);

    Task<OrderDetail> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken = default);

    Task AddOrderItemAsync(int orderId, CreateOrderItemRequest request, CancellationToken cancellationToken = default);

    Task RemoveOrderItemAsync(int orderId, int itemId, CancellationToken cancellationToken = default);

    Task ReplaceOrderItemAsync(int orderId, int itemId, CreateOrderItemRequest request, CancellationToken cancellationToken = default);

    Task ChangeOrderStatusAsync(string orderNumber, string action, string changedBy, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductListItem>> GetProductsAsync(CancellationToken cancellationToken = default);
}
