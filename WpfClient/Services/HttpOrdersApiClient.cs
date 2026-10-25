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

    public async Task<OrderDetail> ChangeOrderStatusAsync(int orderId, string action, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(
            $"api/orders/{orderId}/status",
            new { action },
            cancellationToken);

        await EnsureSuccessWithApiMessageAsync(response, cancellationToken);

        OrderDetailResponse? order = await response.Content.ReadFromJsonAsync<OrderDetailResponse>(cancellationToken);
        return order is null
            ? throw new InvalidOperationException("API не повернув оновлене замовлення.")
            : MapOrderDetail(order);
    }

    public async Task<IReadOnlyList<ProductListItem>> GetProductsAsync(CancellationToken cancellationToken = default)
    {
        List<ProductResponse> products;

        try
        {
            products = await httpClient.GetFromJsonAsync<List<ProductResponse>>("api/products", cancellationToken)
                ?? [];
        }
        catch (HttpRequestException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return LocalCatalog.CreateProducts();
        }

        return products.Select(MapProduct).ToList();
    }

    public async Task<ProductListItem> CreateProductAsync(ProductSaveRequest request, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync("api/products", request, cancellationToken);
        await EnsureSuccessWithApiMessageAsync(response, cancellationToken);

        ProductResponse? product = await response.Content.ReadFromJsonAsync<ProductResponse>(cancellationToken);
        return product is null
            ? throw new InvalidOperationException("API не повернув створений товар.")
            : MapProduct(product);
    }

    public async Task<ProductListItem> UpdateProductAsync(int productId, ProductSaveRequest request, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.PutAsJsonAsync($"api/products/{productId}", request, cancellationToken);
        await EnsureSuccessWithApiMessageAsync(response, cancellationToken);

        ProductResponse? product = await response.Content.ReadFromJsonAsync<ProductResponse>(cancellationToken);
        return product is null
            ? throw new InvalidOperationException("API не повернув оновлений товар.")
            : MapProduct(product);
    }

    public async Task<ProductStock> GetProductStockAsync(int productId, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.GetAsync($"api/products/{productId}/stock", cancellationToken);
        await EnsureSuccessWithApiMessageAsync(response, cancellationToken);

        ProductStockResponse? stock = await response.Content.ReadFromJsonAsync<ProductStockResponse>(cancellationToken);
        return stock is null
            ? throw new InvalidOperationException("API не повернув залишок товару.")
            : new ProductStock { ProductId = stock.ProductId, Quantity = stock.Quantity };
    }

    public async Task<ProductStock> UpdateProductStockAsync(int productId, int quantity, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.PutAsJsonAsync(
            $"api/products/{productId}/stock",
            new { quantity },
            cancellationToken);
        await EnsureSuccessWithApiMessageAsync(response, cancellationToken);

        ProductStockResponse? stock = await response.Content.ReadFromJsonAsync<ProductStockResponse>(cancellationToken);
        return stock is null
            ? throw new InvalidOperationException("API не повернув оновлений залишок товару.")
            : new ProductStock { ProductId = stock.ProductId, Quantity = stock.Quantity };
    }

    public async Task<ReportSummary> GetOrdersReportAsync(DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        OrderReportResponse? report = await httpClient.GetFromJsonAsync<OrderReportResponse>(
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
        TodayOrdersReportResponse? report = await httpClient.GetFromJsonAsync<TodayOrdersReportResponse>(
            $"api/reports/orders/today?date={date:yyyy-MM-dd}",
            cancellationToken);

        if (report is null)
        {
            throw new InvalidOperationException("API не повернув звіт за дату.");
        }

        return (report.Date, report.TotalOrders, report.TotalAmount, report.Orders.Select(MapTodayOrder).ToList());
    }

    public async Task<IReadOnlyList<ProductSalesReportItem>> GetProductSalesReportAsync(DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        List<ProductSalesReportResponse> products = await httpClient.GetFromJsonAsync<List<ProductSalesReportResponse>>(
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
        List<LowStockProductReportResponse> products = await httpClient.GetFromJsonAsync<List<LowStockProductReportResponse>>(
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
        ManagerSummaryReportResponse? report = await httpClient.GetFromJsonAsync<ManagerSummaryReportResponse>(
            $"api/reports/manager-summary{BuildDateRangeQuery(from, to)}",
            cancellationToken);

        return report?.Text ?? string.Empty;
    }

    private static string BuildDateRangeQuery(DateTime? from, DateTime? to)
    {
        List<string> parts = [];

        if (from.HasValue)
        {
            parts.Add($"from={from.Value:yyyy-MM-dd}");
        }

        if (to.HasValue)
        {
            parts.Add($"to={to.Value:yyyy-MM-dd}");
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

    private static ProductListItem MapProduct(ProductResponse product)
    {
        return new ProductListItem
        {
            Id = product.Id,
            Name = product.Name,
            Category = product.CategoryName,
            Price = product.Price,
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

        ApiErrorResponse? error = null;
        try
        {
            error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(cancellationToken);
        }
        catch
        {
            // Fall through to the generic HTTP error when the API did not return JSON.
        }

        string message = string.IsNullOrWhiteSpace(error?.Message)
            ? $"API повернув помилку {(int)response.StatusCode} ({response.ReasonPhrase})."
            : error.Message;

        throw new InvalidOperationException(message);
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
        public DateTime CreatedAt { get; set; }

        public string Text { get; set; } = string.Empty;
    }

    private sealed class ApiErrorResponse
    {
        public string? Message { get; set; }
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
