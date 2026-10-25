using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using System.Globalization;
using WpfClient.Models;

namespace WpfClient.Services;

public sealed class HttpOrdersApiClient(HttpClient httpClient) : IOrdersApiClient, IAuthApiClient, IManagementApiClient, IOrderEditingApiClient
{
    private DateTimeOffset? _expiresAt;
    private CancellationTokenSource _session = new();
    public event Action? SessionExpired;

    public async Task<AuthenticatedUser> SignInAsync(string login, string password, CancellationToken cancellationToken = default)
    {
        SignOut();
        using HttpResponseMessage loginResponse = await PostJsonAsync(
            "api/auth/login",
            new LoginRequest(login, password),
            cancellationToken);

        LoginResponse? token = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken);
        if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
        {
            throw new InvalidOperationException("API не повернув токен авторизації.");
        }

        _expiresAt = token.AccessTokenExpiresAt;
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            token.TokenType,
            token.AccessToken);

        try
        {
            CurrentUserResponse? currentUser = await GetJsonAsync<CurrentUserResponse>("api/auth/me", cancellationToken);
            return currentUser is null
                ? throw new InvalidOperationException("API не повернув поточного користувача.")
                : new AuthenticatedUser
                {
                    Login = currentUser.Login,
                    FullName = currentUser.FullName,
                    Role = currentUser.Role,
                    Id = currentUser.Id,
                    AccessTokenExpiresAt = token.AccessTokenExpiresAt
                };
        }
        catch
        {
            SignOut();
            throw;
        }
    }

    public void SignOut()
    {
        _session.Cancel();
        _session.Dispose();
        _session = new();
        _expiresAt = null;
        httpClient.DefaultRequestHeaders.Authorization = null;
    }

    public async Task<IReadOnlyList<OrderListItem>> GetOrdersAsync(CancellationToken cancellationToken = default)
    {
        List<OrderSummaryResponse> orders = await GetJsonAsync<List<OrderSummaryResponse>>("api/orders", cancellationToken)
            ?? [];

        return orders.Select(MapOrderSummary).ToList();
    }

    public async Task<OrderDetail?> GetOrderDetailsAsync(int orderId, CancellationToken cancellationToken = default)
    {
        try
        {
            OrderDetailResponse? order = await GetJsonAsync<OrderDetailResponse>($"api/orders/{orderId}", cancellationToken);
            return order is null ? null : MapOrderDetail(order);
        }
        catch (ApiException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<OrderDetail> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await PostJsonAsync("api/orders", request, cancellationToken);

        OrderDetailResponse? order = await response.Content.ReadFromJsonAsync<OrderDetailResponse>(cancellationToken);
        return order is null
            ? throw new InvalidOperationException("API не повернув створене замовлення.")
            : MapOrderDetail(order);
    }

    public async Task AddOrderItemAsync(int orderId, CreateOrderItemRequest request, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await PostJsonAsync($"api/orders/{orderId}/items", request, cancellationToken);
    }

    public async Task RemoveOrderItemAsync(int orderId, int itemId, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await DeleteAsync($"api/orders/{orderId}/items/{itemId}", cancellationToken);
    }

    public async Task ReplaceOrderItemAsync(int orderId, int itemId, CreateOrderItemRequest request, CancellationToken cancellationToken = default)
    {
        OrderDetail order = await GetOrderDetailsAsync(orderId, cancellationToken)
            ?? throw new InvalidOperationException("Order was not found.");
        if (!order.Items.Any(i => i.Id == itemId && i.ProductId == request.ProductId))
            throw new InvalidOperationException("Order item was not found or its product has changed.");
        await UpdateOrderAsync(orderId, new(order.Version, order.Comment, order.Items.Select(i =>
            new UpdateOrderItemRequest(i.Id, i.ProductId, i.Id == itemId ? request.Quantity : i.Quantity)).ToList()), cancellationToken);
    }

    public async Task<OrderDetail> UpdateOrderAsync(int id, UpdateOrderRequest request, CancellationToken ct = default)
    {
        using var response = await PutJsonAsync($"api/orders/{id}", request, ct);
        var result = await response.Content.ReadFromJsonAsync<OrderDetailResponse>(ct)
            ?? throw new InvalidOperationException("API did not return the updated order.");
        return MapOrderDetail(result);
    }

    public async Task<IReadOnlyList<CustomerListItem>> GetCustomersAsync(CancellationToken ct = default) =>
        await GetJsonAsync<List<CustomerListItem>>("api/customers", ct) ?? [];

    public async Task<CustomerListItem> SaveCustomerAsync(int? id, SaveCustomerRequest request, CancellationToken ct = default)
    {
        using var response = id.HasValue ? await PutJsonAsync($"api/customers/{id}", request, ct) : await PostJsonAsync("api/customers", request, ct);
        return await response.Content.ReadFromJsonAsync<CustomerListItem>(ct) ?? throw new InvalidOperationException("API did not return the customer.");
    }

    public async Task<IReadOnlyList<UserAccount>> GetUsersAsync(CancellationToken ct = default) =>
        await GetJsonAsync<List<UserAccount>>("api/users", ct) ?? [];

    public async Task<UserAccount> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        using var response = await PostJsonAsync("api/users", request, ct);
        return await response.Content.ReadFromJsonAsync<UserAccount>(ct) ?? throw new InvalidOperationException("API did not return the user.");
    }

    public async Task<UserAccount> UpdateUserAsync(int id, SaveUserRequest request, CancellationToken ct = default)
    {
        using var response = await PutJsonAsync($"api/users/{id}", request, ct);
        return await response.Content.ReadFromJsonAsync<UserAccount>(ct) ?? throw new InvalidOperationException("API did not return the user.");
    }

    public async Task ChangeUserPasswordAsync(int id, string password, CancellationToken ct = default)
    {
        using var response = await PutJsonAsync($"api/users/{id}/password", new { password }, ct);
    }

    public async Task<OrderDetail> ChangeOrderStatusAsync(int orderId, string action, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await PostJsonAsync(
            $"api/orders/{orderId}/status",
            new { action },
            cancellationToken);


        OrderDetailResponse? order = await response.Content.ReadFromJsonAsync<OrderDetailResponse>(cancellationToken);
        return order is null
            ? throw new InvalidOperationException("API не повернув оновлене замовлення.")
            : MapOrderDetail(order);
    }

    public async Task<IReadOnlyList<ProductListItem>> GetProductsAsync(CancellationToken cancellationToken = default)
    {
        List<ProductResponse> products = await GetJsonAsync<List<ProductResponse>>("api/products", cancellationToken) ?? [];

        return products.Select(MapProduct).ToList();
    }

    public async Task<ProductListItem> CreateProductAsync(ProductSaveRequest request, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await PostJsonAsync("api/products", request, cancellationToken);

        ProductResponse? product = await response.Content.ReadFromJsonAsync<ProductResponse>(cancellationToken);
        return product is null
            ? throw new InvalidOperationException("API не повернув створений товар.")
            : MapProduct(product);
    }

    public async Task<ProductListItem> UpdateProductAsync(int productId, ProductSaveRequest request, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await PutJsonAsync($"api/products/{productId}", request, cancellationToken);

        ProductResponse? product = await response.Content.ReadFromJsonAsync<ProductResponse>(cancellationToken);
        return product is null
            ? throw new InvalidOperationException("API не повернув оновлений товар.")
            : MapProduct(product);
    }

    public async Task<ProductStock> GetProductStockAsync(int productId, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await GetAsync($"api/products/{productId}/stock", cancellationToken);

        ProductStockResponse? stock = await response.Content.ReadFromJsonAsync<ProductStockResponse>(cancellationToken);
        return stock is null
            ? throw new InvalidOperationException("API не повернув залишок товару.")
            : new ProductStock { ProductId = stock.ProductId, Quantity = stock.Quantity };
    }

    public async Task<ProductStock> UpdateProductStockAsync(int productId, int quantity, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await PutJsonAsync(
            $"api/products/{productId}/stock",
            new { quantity },
            cancellationToken);

        ProductStockResponse? stock = await response.Content.ReadFromJsonAsync<ProductStockResponse>(cancellationToken);
        return stock is null
            ? throw new InvalidOperationException("API не повернув оновлений залишок товару.")
            : new ProductStock { ProductId = stock.ProductId, Quantity = stock.Quantity };
    }

    public async Task<ReportSummary> GetOrdersReportAsync(DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        OrderReportResponse? report = await GetJsonAsync<OrderReportResponse>(
            $"api/reports/orders{BuildDateRangeQuery(from, to)}",
            cancellationToken);
        return report is null
            ? throw new InvalidOperationException("API не повернув звіт по замовленнях.")
            : new ReportSummary
            {
                TotalOrders = report.TotalOrders,
                NewOrders = report.NewOrders,
                OrdersInProgress = report.OrdersInProgress,
                ShippedOrders = report.ShippedOrders,
                InvoicedOrders = report.InvoicedOrders,
                CancelledOrders = report.CancelledOrders,
                TotalAmount = report.TotalAmount,
                AverageOrderAmount = report.AverageOrderAmount,
                LowStockProducts = report.LowStockProducts,
                TopProductName = report.TopProductName,
                TopProductQuantity = report.TopProductQuantity
            };
    }

    public async Task<(DateTime Date, int TotalOrders, decimal TotalAmount, IReadOnlyList<TodayOrderReportItem> Orders)> GetTodayOrdersReportAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        TodayOrdersReportResponse? report = await GetJsonAsync<TodayOrdersReportResponse>(
            "api/reports/orders/today?date=" + date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            cancellationToken);

        if (report is null)
        {
            throw new InvalidOperationException("API не повернув звіт за дату.");
        }

        return (report.Date, report.TotalOrders, report.TotalAmount, report.Orders.Select(MapTodayOrder).ToList());
    }

    public async Task<IReadOnlyList<ProductSalesReportItem>> GetProductSalesReportAsync(DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        List<ProductSalesReportResponse> products = await GetJsonAsync<List<ProductSalesReportResponse>>(
            $"api/reports/products{BuildDateRangeQuery(from, to)}",
            cancellationToken) ?? [];

        return products.Select(item => new ProductSalesReportItem
        {
            ProductId = item.ProductId,
            ProductName = item.ProductName,
            CategoryName = item.CategoryName,
            Quantity = item.Quantity,
            TotalAmount = item.TotalAmount
        }).ToList();
    }

    public async Task<IReadOnlyList<LowStockProductReportItem>> GetLowStockReportAsync(CancellationToken cancellationToken = default)
    {
        List<LowStockProductReportResponse> products = await GetJsonAsync<List<LowStockProductReportResponse>>(
            "api/reports/low-stock",
            cancellationToken) ?? [];

        return products.Select(item => new LowStockProductReportItem
        {
            ProductId = item.ProductId,
            ProductName = item.ProductName,
            CategoryName = item.CategoryName,
            Quantity = item.Quantity,
            Price = item.Price
        }).ToList();
    }

    public async Task<string> GetManagerSummaryReportAsync(DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        ManagerSummaryReportResponse? report = await GetJsonAsync<ManagerSummaryReportResponse>(
            $"api/reports/manager-summary{BuildDateRangeQuery(from, to)}",
            cancellationToken);

        return report?.Text ?? string.Empty;
    }

    private static string BuildDateRangeQuery(DateTime? from, DateTime? to)
    {
        if (from?.Date > to?.Date)
            throw new ArgumentException(LocalizationManager.Instance["InvalidReportRange"]);
        List<string> parts = [];

        if (from.HasValue)
        {
            parts.Add("from=" + from.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        if (to.HasValue)
        {
            parts.Add("to=" + to.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        return parts.Count == 0 ? string.Empty : $"?{string.Join("&", parts)}";
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
            IsStatusFinal = order.IsStatusFinal,
            CurrencyCode = order.CurrencyCode,
            Total = order.TotalAmount
        };
    }

    private static OrderDetail MapOrderDetail(OrderDetailResponse order)
    {
        return new OrderDetail
        {
            Version = order.Version,
            Id = order.Id,
            CustomerId = order.CustomerId,
            Number = order.OrderNumber,
            CreatedAt = order.CreatedAt,
            Customer = order.CustomerFullName,
            Status = order.StatusCode,
            IsStatusFinal = order.IsStatusFinal,
            CurrencyCode = order.CurrencyCode,
            Total = order.TotalAmount,
            Comment = order.Comment,
            CustomerPhone = order.CustomerPhone,
            CustomerEmail = order.CustomerEmail,
            CustomerAddress = order.CustomerAddress,
            UpdatedAt = order.UpdatedAt,
            CreatedByUserId = order.CreatedByUserId,
            Items = order.Items.Select(item => new OrderDetailItem
            {
                Id = item.Id,
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                CurrencyCode = item.CurrencyCode,
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

    private static ProductListItem MapProduct(ProductResponse product)
    {
        return new ProductListItem
        {
            Id = product.Id,
            Name = product.Name,
            Category = product.CategoryName,
            Price = product.Price,
            CurrencyCode = product.CurrencyCode,
            Quantity = product.Quantity,
            IsActive = product.IsActive
        };
    }

    private static TodayOrderReportItem MapTodayOrder(OrderReportItemResponse order)
    {
        return new TodayOrderReportItem
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            CustomerFullName = order.CustomerFullName,
            StatusCode = order.StatusCode,
            StatusName = order.StatusName,
            TotalAmount = order.TotalAmount,
            CreatedAt = order.CreatedAt
        };
    }

    private static async Task EnsureSuccessWithApiMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        List<string> messages = [];
        try
        {
            using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (json.RootElement.ValueKind != JsonValueKind.Object)
                throw new JsonException("Expected an error object.");
            foreach (string key in new[] { "message", "detail", "title" })
                if (json.RootElement.TryGetProperty(key, out JsonElement value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
                {
                    messages.Add(value.GetString()!);
                    break;
                }
            if (json.RootElement.TryGetProperty("errors", out JsonElement errors) && errors.ValueKind == JsonValueKind.Object)
                foreach (JsonProperty field in errors.EnumerateObject())
                    if (field.Value.ValueKind == JsonValueKind.Array)
                        messages.AddRange(field.Value.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.String).Select(v => v.GetString()!));
        }
        catch (JsonException)
        {
            // Fall through to the generic HTTP error when the API did not return JSON.
        }

        string message = messages.Count == 0
            ? $"API повернув помилку {(int)response.StatusCode} ({response.ReasonPhrase})."
            : string.Join(" ", messages.Distinct());

        throw new ApiException(message, response.StatusCode);
    }

    private async Task<T?> GetJsonAsync<T>(string path, CancellationToken ct)
    {
        using HttpResponseMessage response = await SendAsync(HttpMethod.Get, path, null, ct);
        return await response.Content.ReadFromJsonAsync<T>(ct);
    }

    private Task<HttpResponseMessage> PostJsonAsync<T>(string path, T body, CancellationToken ct) =>
        SendAsync(HttpMethod.Post, path, JsonContent.Create(body), ct);

    private Task<HttpResponseMessage> PutJsonAsync<T>(string path, T body, CancellationToken ct) =>
        SendAsync(HttpMethod.Put, path, JsonContent.Create(body), ct);

    private Task<HttpResponseMessage> GetAsync(string path, CancellationToken ct) => SendAsync(HttpMethod.Get, path, null, ct);
    private Task<HttpResponseMessage> DeleteAsync(string path, CancellationToken ct) => SendAsync(HttpMethod.Delete, path, null, ct);

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, HttpContent? body, CancellationToken ct)
    {
        using HttpRequestMessage request = new(method, path) { Content = body };
        bool login = path == "api/auth/login";
        if (!login && httpClient.DefaultRequestHeaders.Authorization is null)
            throw new ApiException(LocalizationManager.Instance["SessionExpired"], HttpStatusCode.Unauthorized);
        if (!login && _expiresAt <= DateTimeOffset.UtcNow)
        {
            ExpireSession();
            throw new ApiException(LocalizationManager.Instance["SessionExpired"], HttpStatusCode.Unauthorized);
        }
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _session.Token);
        HttpResponseMessage response = await httpClient.SendAsync(request, linked.Token);
        try
        {
            if (!login && response.StatusCode == HttpStatusCode.Unauthorized)
                ExpireSession();
            await EnsureSuccessWithApiMessageAsync(response, ct);
            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    private void ExpireSession()
    {
        SignOut();
        SessionExpired?.Invoke();
    }

    private sealed class OrderSummaryResponse
    {
        public int Id { get; set; }

        public string OrderNumber { get; set; } = string.Empty;

        public string StatusCode { get; set; } = string.Empty;
        public bool IsStatusFinal { get; set; }
        public string CurrencyCode { get; set; } = "UAH";

        public string CustomerFullName { get; set; } = string.Empty;

        public int CustomerId { get; set; }

        public decimal TotalAmount { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    private sealed class OrderDetailResponse
    {
        public string Version { get; set; } = "";
        public string? CustomerPhone { get; set; }
        public string? CustomerEmail { get; set; }
        public string? CustomerAddress { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int CreatedByUserId { get; set; }
        public int Id { get; set; }

        public string OrderNumber { get; set; } = string.Empty;

        public string StatusCode { get; set; } = string.Empty;
        public bool IsStatusFinal { get; set; }
        public string CurrencyCode { get; set; } = "UAH";

        public string CustomerFullName { get; set; } = string.Empty;

        public int CustomerId { get; set; }

        public decimal TotalAmount { get; set; }

        public string? Comment { get; set; }

        public DateTime CreatedAt { get; set; }

        public List<OrderItemResponse> Items { get; set; } = [];

        public List<OrderStatusHistoryResponse> StatusHistory { get; set; } = [];
    }

    private sealed class ProductResponse
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string CategoryName { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public string CurrencyCode { get; set; } = string.Empty;

        public int Quantity { get; set; }

        public bool IsActive { get; set; }
    }

    private sealed class ProductStockResponse
    {
        public int ProductId { get; set; }

        public int Quantity { get; set; }
    }

    private sealed class OrderReportResponse
    {
        public int TotalOrders { get; set; }

        public int NewOrders { get; set; }

        public int OrdersInProgress { get; set; }

        public int ShippedOrders { get; set; }

        public int InvoicedOrders { get; set; }

        public int CancelledOrders { get; set; }

        public decimal TotalAmount { get; set; }

        public decimal AverageOrderAmount { get; set; }

        public int LowStockProducts { get; set; }

        public string TopProductName { get; set; } = string.Empty;

        public int TopProductQuantity { get; set; }
    }

    private sealed class TodayOrdersReportResponse
    {
        public DateTime Date { get; set; }

        public int TotalOrders { get; set; }

        public decimal TotalAmount { get; set; }

        public List<OrderReportItemResponse> Orders { get; set; } = [];
    }

    private sealed class OrderReportItemResponse
    {
        public int Id { get; set; }

        public string OrderNumber { get; set; } = string.Empty;

        public string CustomerFullName { get; set; } = string.Empty;

        public string StatusCode { get; set; } = string.Empty;
        public bool IsStatusFinal { get; set; }
        public string CurrencyCode { get; set; } = "UAH";

        public string StatusName { get; set; } = string.Empty;

        public decimal TotalAmount { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    private sealed class ProductSalesReportResponse
    {
        public int ProductId { get; set; }

        public string ProductName { get; set; } = string.Empty;

        public string CategoryName { get; set; } = string.Empty;

        public int Quantity { get; set; }

        public decimal TotalAmount { get; set; }
    }

    private sealed class LowStockProductReportResponse
    {
        public int ProductId { get; set; }

        public string ProductName { get; set; } = string.Empty;

        public string CategoryName { get; set; } = string.Empty;

        public int Quantity { get; set; }

        public decimal Price { get; set; }
    }

    private sealed class ManagerSummaryReportResponse
    {
        public DateTimeOffset? GeneratedAt { get; set; }
        public DateTimeOffset? CreatedAt { get; set; }

        public string Text { get; set; } = string.Empty;
    }

    private sealed class OrderItemResponse
    {
        public string CurrencyCode { get; set; } = "UAH";
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
        public DateTimeOffset? AccessTokenExpiresAt { get; set; }
        public string AccessToken { get; set; } = string.Empty;

        public string TokenType { get; set; } = "Bearer";
    }

    private sealed class CurrentUserResponse
    {
        public int Id { get; set; }
        public string Login { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;
    }
}
