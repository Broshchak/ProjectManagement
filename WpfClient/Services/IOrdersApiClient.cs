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

    Task<OrderDetail> ChangeOrderStatusAsync(int orderId, string action, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductListItem>> GetProductsAsync(CancellationToken cancellationToken = default);

    Task<ProductListItem> CreateProductAsync(ProductSaveRequest request, CancellationToken cancellationToken = default);

    Task<ProductListItem> UpdateProductAsync(int productId, ProductSaveRequest request, CancellationToken cancellationToken = default);

    Task<ProductStock> GetProductStockAsync(int productId, CancellationToken cancellationToken = default);

    Task<ProductStock> UpdateProductStockAsync(int productId, int quantity, CancellationToken cancellationToken = default);
}
