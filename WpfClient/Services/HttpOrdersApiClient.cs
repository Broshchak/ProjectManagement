using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using WpfClient.Models;

namespace WpfClient.Services;

public sealed class HttpOrdersApiClient(HttpClient httpClient) : IOrdersApiClient, IAuthApiClient
{
    public async Task<AuthenticatedUser> SignInAsync(string login, string password, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage loginResponse = await httpClient.PostAsJsonAsync(
            "api/auth/login",
            new LoginRequest(login, password),
            cancellationToken);

        loginResponse.EnsureSuccessStatusCode();

        LoginResponse? token = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken);
        if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
        {
            throw new InvalidOperationException("API не повернув токен авторизації.");
        }

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            token.TokenType,
            token.AccessToken);

        CurrentUserResponse? currentUser = await httpClient.GetFromJsonAsync<CurrentUserResponse>("api/auth/me", cancellationToken);
        return currentUser is null
            ? throw new InvalidOperationException("API не повернув поточного користувача.")
            : new AuthenticatedUser
            {
                Login = currentUser.Login,
                FullName = currentUser.FullName,
                Role = currentUser.Role
            };
    }

    public void SignOut()
    {
        httpClient.DefaultRequestHeaders.Authorization = null;
    }

    public async Task<IReadOnlyList<OrderListItem>> GetOrdersAsync(CancellationToken cancellationToken = default)
    {
        List<OrderSummaryResponse> orders = await httpClient.GetFromJsonAsync<List<OrderSummaryResponse>>("api/orders", cancellationToken)
            ?? [];

        return orders.Select(MapOrderSummary).ToList();
    }

    public async Task<OrderDetail?> GetOrderDetailsAsync(int orderId, CancellationToken cancellationToken = default)
    {
        OrderDetailResponse? order = await httpClient.GetFromJsonAsync<OrderDetailResponse>($"api/orders/{orderId}", cancellationToken);
        return order is null ? null : MapOrderDetail(order);
    }

    public async Task<OrderDetail> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync("api/orders", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        OrderDetailResponse? order = await response.Content.ReadFromJsonAsync<OrderDetailResponse>(cancellationToken);
        return order is null
            ? throw new InvalidOperationException("API не повернув створене замовлення.")
            : MapOrderDetail(order);
    }

    public async Task AddOrderItemAsync(int orderId, CreateOrderItemRequest request, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync($"api/orders/{orderId}/items", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task RemoveOrderItemAsync(int orderId, int itemId, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.DeleteAsync($"api/orders/{orderId}/items/{itemId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ReplaceOrderItemAsync(int orderId, int itemId, CreateOrderItemRequest request, CancellationToken cancellationToken = default)
    {
        await RemoveOrderItemAsync(orderId, itemId, cancellationToken);
        await AddOrderItemAsync(orderId, request, cancellationToken);
    }

    public async Task ChangeOrderStatusAsync(string orderNumber, string action, string changedBy, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(
            $"api/orders/{Uri.EscapeDataString(orderNumber)}/{Uri.EscapeDataString(action)}",
            new { changedBy },
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<ProductListItem>> GetProductsAsync(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return LocalCatalog.CreateProducts();
    }

    private static OrderListItem MapOrderSummary(OrderSummaryResponse order)
    {
        return new OrderListItem
        {
            Id = order.Id,
            CustomerId = order.CustomerId,
            Number = order.OrderNumber,
            CreatedAt = order.CreatedAt,
            Customer = order.CustomerFullName,
            Status = order.StatusCode,
            Total = order.TotalAmount
        };
    }

    private static OrderDetail MapOrderDetail(OrderDetailResponse order)
    {
        return new OrderDetail
        {
            Id = order.Id,
            CustomerId = order.CustomerId,
            Number = order.OrderNumber,
            CreatedAt = order.CreatedAt,
            Customer = order.CustomerFullName,
            Status = order.StatusCode,
            Total = order.TotalAmount,
            Comment = order.Comment,
            Items = order.Items.Select(item => new OrderDetailItem
            {
                Id = item.Id,
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                LineTotal = item.LineTotal
            }).ToList(),
            StatusHistory = order.StatusHistory.Select(history => new OrderStatusHistoryItem
            {
                PreviousStatus = history.PreviousStatusCode ?? "-",
                NewStatus = history.NewStatusCode,
                ChangedBy = history.ChangedByUserFullName,
                ChangedAt = history.ChangedAt,
                Comment = history.Comment
            }).ToList()
        };
    }

    private sealed class OrderSummaryResponse
    {
        public int Id { get; set; }

        public string OrderNumber { get; set; } = string.Empty;

        public string StatusCode { get; set; } = string.Empty;

        public string CustomerFullName { get; set; } = string.Empty;

        public int CustomerId { get; set; }

        public decimal TotalAmount { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    private sealed class OrderDetailResponse
    {
        public int Id { get; set; }

        public string OrderNumber { get; set; } = string.Empty;

        public string StatusCode { get; set; } = string.Empty;

        public string CustomerFullName { get; set; } = string.Empty;

        public int CustomerId { get; set; }

        public decimal TotalAmount { get; set; }

        public string? Comment { get; set; }

        public DateTime CreatedAt { get; set; }

        public List<OrderItemResponse> Items { get; set; } = [];

        public List<OrderStatusHistoryResponse> StatusHistory { get; set; } = [];
    }

    private sealed class OrderItemResponse
    {
        public int Id { get; set; }

        public int ProductId { get; set; }

        public string ProductName { get; set; } = string.Empty;

        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        public decimal LineTotal { get; set; }
    }

    private sealed class OrderStatusHistoryResponse
    {
        public string? PreviousStatusCode { get; set; }

        public string NewStatusCode { get; set; } = string.Empty;

        public string ChangedByUserFullName { get; set; } = string.Empty;

        public DateTime ChangedAt { get; set; }

        public string? Comment { get; set; }
    }

    private sealed record LoginRequest(string Login, string Password);

    private sealed class LoginResponse
    {
        public string AccessToken { get; set; } = string.Empty;

        public string TokenType { get; set; } = "Bearer";
    }

    private sealed class CurrentUserResponse
    {
        public string Login { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;
    }
}
