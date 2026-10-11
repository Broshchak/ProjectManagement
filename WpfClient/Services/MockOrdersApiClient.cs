using WpfClient.Models;

namespace WpfClient.Services;

public sealed class MockOrdersApiClient : IOrdersApiClient
{
    private readonly List<OrderListItem> _orders =
    [
        new() { Id = 1, CustomerId = 1, Number = "ORD-0001", CreatedAt = new DateTime(2026, 9, 1), Customer = "Demo Customer", Status = "NewOrder", Total = 90m },
        new() { Id = 2, CustomerId = 2, Number = "ORD-0002", CreatedAt = new DateTime(2026, 9, 3), Customer = "Lviv Office", Status = "Registered", Total = 475m },
        new() { Id = 3, CustomerId = 3, Number = "ORD-0003", CreatedAt = new DateTime(2026, 9, 5), Customer = "Student Lab", Status = "Granted", Total = 860m },
        new() { Id = 4, CustomerId = 4, Number = "ORD-0004", CreatedAt = new DateTime(2026, 9, 8), Customer = "Library", Status = "Shipped", Total = 2400m },
        new() { Id = 5, CustomerId = 5, Number = "ORD-0005", CreatedAt = new DateTime(2026, 9, 10), Customer = "Dean Office", Status = "Invoiced", Total = 725m }
    ];

    private readonly Dictionary<int, OrderDetail> _orderDetails = new()
    {
        [1] = new()
        {
            Id = 1,
            CustomerId = 1,
            Number = "ORD-0001",
            CreatedAt = new DateTime(2026, 9, 1),
            Customer = "Demo Customer",
            Status = "NewOrder",
            Total = 90m,
            Comment = "Demo order for first run",
            Items =
            [
                new() { Id = 1, ProductId = 1, ProductName = "Notebook A5", Quantity = 2, UnitPrice = 45m, LineTotal = 90m }
            ],
            StatusHistory =
            [
                new() { PreviousStatus = "-", NewStatus = "NewOrder", ChangedBy = "System Administrator", ChangedAt = new DateTime(2026, 9, 1, 9, 15, 0), Comment = "Initial status" }
            ]
        },
        [2] = new()
        {
            Id = 2,
            CustomerId = 2,
            Number = "ORD-0002",
            CreatedAt = new DateTime(2026, 9, 3),
            Customer = "Lviv Office",
            Status = "Registered",
            Total = 475m,
            Comment = "Office supplies restock",
            Items =
            [
                new() { Id = 2, ProductId = 2, ProductName = "Blue pen", Quantity = 10, UnitPrice = 12.5m, LineTotal = 125m },
                new() { Id = 3, ProductId = 3, ProductName = "Folder", Quantity = 10, UnitPrice = 18m, LineTotal = 180m },
                new() { Id = 4, ProductId = 1, ProductName = "Notebook A5", Quantity = 4, UnitPrice = 42.5m, LineTotal = 170m }
            ],
            StatusHistory =
            [
                new() { PreviousStatus = "-", NewStatus = "NewOrder", ChangedBy = "Order Manager", ChangedAt = new DateTime(2026, 9, 3, 10, 0, 0), Comment = "Created" },
                new() { PreviousStatus = "NewOrder", NewStatus = "Registered", ChangedBy = "Order Manager", ChangedAt = new DateTime(2026, 9, 3, 10, 30, 0), Comment = "Registered for processing" }
            ]
        },
        [3] = new()
        {
            Id = 3,
            CustomerId = 3,
            Number = "ORD-0003",
            CreatedAt = new DateTime(2026, 9, 5),
            Customer = "Student Lab",
            Status = "Granted",
            Total = 860m,
            Comment = "Equipment for lab class",
            Items =
            [
                new() { Id = 5, ProductId = 4, ProductName = "USB flash drive 32GB", Quantity = 2, UnitPrice = 220m, LineTotal = 440m },
                new() { Id = 6, ProductId = 5, ProductName = "Wireless mouse", Quantity = 1, UnitPrice = 420m, LineTotal = 420m }
            ],
            StatusHistory =
            [
                new() { PreviousStatus = "-", NewStatus = "NewOrder", ChangedBy = "Order Manager", ChangedAt = new DateTime(2026, 9, 5, 8, 45, 0), Comment = "Created" },
                new() { PreviousStatus = "NewOrder", NewStatus = "Registered", ChangedBy = "Order Manager", ChangedAt = new DateTime(2026, 9, 5, 9, 5, 0), Comment = "Registered" },
                new() { PreviousStatus = "Registered", NewStatus = "Granted", ChangedBy = "Company Director", ChangedAt = new DateTime(2026, 9, 5, 11, 20, 0), Comment = "Approved" }
            ]
        },
        [4] = new()
        {
            Id = 4,
            CustomerId = 4,
            Number = "ORD-0004",
            CreatedAt = new DateTime(2026, 9, 8),
            Customer = "Library",
            Status = "Shipped",
            Total = 2400m,
            Comment = "Furniture delivery",
            Items =
            [
                new() { Id = 7, ProductId = 6, ProductName = "Office chair", Quantity = 1, UnitPrice = 2400m, LineTotal = 2400m }
            ],
            StatusHistory =
            [
                new() { PreviousStatus = "-", NewStatus = "NewOrder", ChangedBy = "Order Manager", ChangedAt = new DateTime(2026, 9, 8, 12, 10, 0), Comment = "Created" },
                new() { PreviousStatus = "NewOrder", NewStatus = "Registered", ChangedBy = "Order Manager", ChangedAt = new DateTime(2026, 9, 8, 12, 25, 0), Comment = "Registered" },
                new() { PreviousStatus = "Registered", NewStatus = "Granted", ChangedBy = "Company Director", ChangedAt = new DateTime(2026, 9, 8, 13, 0, 0), Comment = "Approved" },
                new() { PreviousStatus = "Granted", NewStatus = "Shipped", ChangedBy = "Order Manager", ChangedAt = new DateTime(2026, 9, 9, 9, 0, 0), Comment = "Sent to customer" }
            ]
        },
        [5] = new()
        {
            Id = 5,
            CustomerId = 5,
            Number = "ORD-0005",
            CreatedAt = new DateTime(2026, 9, 10),
            Customer = "Dean Office",
            Status = "Invoiced",
            Total = 725m,
            Comment = "Monthly supplies",
            Items =
            [
                new() { Id = 8, ProductId = 3, ProductName = "Folder", Quantity = 5, UnitPrice = 18m, LineTotal = 90m },
                new() { Id = 9, ProductId = 1, ProductName = "Notebook A5", Quantity = 5, UnitPrice = 45m, LineTotal = 225m },
                new() { Id = 10, ProductId = 5, ProductName = "Wireless mouse", Quantity = 1, UnitPrice = 410m, LineTotal = 410m }
            ],
            StatusHistory =
            [
                new() { PreviousStatus = "-", NewStatus = "NewOrder", ChangedBy = "System Administrator", ChangedAt = new DateTime(2026, 9, 10, 8, 15, 0), Comment = "Created" },
                new() { PreviousStatus = "NewOrder", NewStatus = "Registered", ChangedBy = "Order Manager", ChangedAt = new DateTime(2026, 9, 10, 8, 30, 0), Comment = "Registered" },
                new() { PreviousStatus = "Registered", NewStatus = "Granted", ChangedBy = "Company Director", ChangedAt = new DateTime(2026, 9, 10, 9, 10, 0), Comment = "Approved" },
                new() { PreviousStatus = "Granted", NewStatus = "Shipped", ChangedBy = "Order Manager", ChangedAt = new DateTime(2026, 9, 10, 14, 0, 0), Comment = "Delivered" },
                new() { PreviousStatus = "Shipped", NewStatus = "Invoiced", ChangedBy = "System Administrator", ChangedAt = new DateTime(2026, 9, 11, 10, 0, 0), Comment = "Invoice issued" }
            ]
        }
    };

    private int _nextOrderId = 6;
    private int _nextOrderItemId = 11;

    private readonly List<ProductListItem> _products = LocalCatalog.CreateProducts().ToList();

    public async Task<IReadOnlyList<OrderListItem>> GetOrdersAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(150, cancellationToken);
        return _orders;
    }

    public async Task<OrderDetail?> GetOrderDetailsAsync(int orderId, CancellationToken cancellationToken = default)
    {
        await Task.Delay(150, cancellationToken);
        return _orderDetails.GetValueOrDefault(orderId);
    }

    public async Task<OrderDetail> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken = default)
    {
        await Task.Delay(150, cancellationToken);

        int orderId = _nextOrderId++;
        OrderDetail order = new()
        {
            Id = orderId,
            Number = request.OrderNumber,
            CreatedAt = DateTime.Now,
            CustomerId = request.CustomerId,
            Customer = LocalCatalog.GetCustomerName(request.CustomerId),
            Status = "NewOrder",
            Comment = request.Comment,
            Items = request.Items.Select(CreateMockItem).ToList(),
            StatusHistory =
            [
                new()
                {
                    PreviousStatus = "-",
                    NewStatus = "NewOrder",
                    ChangedBy = "System Administrator",
                    ChangedAt = DateTime.Now,
                    Comment = "Initial order creation"
                }
            ]
        };
        order.Total = order.Items.Sum(item => item.LineTotal);

        _orderDetails[orderId] = order;
        _orders.Insert(0, new OrderListItem
        {
            Id = order.Id,
            CustomerId = order.CustomerId,
            Number = order.Number,
            CreatedAt = order.CreatedAt,
            Customer = order.Customer,
            Status = order.Status,
            Total = order.Total
        });

        return order;
    }

    public async Task AddOrderItemAsync(int orderId, CreateOrderItemRequest request, CancellationToken cancellationToken = default)
    {
        await Task.Delay(150, cancellationToken);

        if (!_orderDetails.TryGetValue(orderId, out OrderDetail? order))
        {
            throw new InvalidOperationException("Замовлення не знайдено.");
        }

        order.Items.Add(CreateMockItem(request));
        RecalculateOrderTotal(order);
    }

    public async Task RemoveOrderItemAsync(int orderId, int itemId, CancellationToken cancellationToken = default)
    {
        await Task.Delay(150, cancellationToken);

        if (!_orderDetails.TryGetValue(orderId, out OrderDetail? order))
        {
            throw new InvalidOperationException("Замовлення не знайдено.");
        }

        OrderDetailItem? item = order.Items.FirstOrDefault(orderItem => orderItem.Id == itemId);
        if (item is null)
        {
            throw new InvalidOperationException("Позицію замовлення не знайдено.");
        }

        order.Items.Remove(item);
        RecalculateOrderTotal(order);
    }

    public async Task ReplaceOrderItemAsync(int orderId, int itemId, CreateOrderItemRequest request, CancellationToken cancellationToken = default)
    {
        await RemoveOrderItemAsync(orderId, itemId, cancellationToken);
        await AddOrderItemAsync(orderId, request, cancellationToken);
    }

    public async Task<OrderDetail> ChangeOrderStatusAsync(int orderId, string action, CancellationToken cancellationToken = default)
    {
        await Task.Delay(150, cancellationToken);

        OrderDetail? orderDetails = _orderDetails.GetValueOrDefault(orderId);
        if (orderDetails is null)
        {
            throw new InvalidOperationException("Замовлення не знайдено.");
        }

        string nextStatus = GetNextStatus(orderDetails.Status, action);
        string previousStatus = orderDetails.Status;

        orderDetails.Status = nextStatus;
        orderDetails.StatusHistory.Add(new OrderStatusHistoryItem
        {
            PreviousStatus = previousStatus,
            NewStatus = nextStatus,
            ChangedBy = "System",
            ChangedAt = DateTime.Now,
            Comment = GetActionComment(action)
        });

        OrderListItem? listItem = _orders.FirstOrDefault(order => order.Id == orderId);
        if (listItem is not null)
        {
            listItem.Status = nextStatus;
        }

        return orderDetails;
    }

    public async Task<IReadOnlyList<ProductListItem>> GetProductsAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(150, cancellationToken);
        return _products;
    }

    public async Task<ProductListItem> CreateProductAsync(ProductSaveRequest request, CancellationToken cancellationToken = default)
    {
        await Task.Delay(150, cancellationToken);

        ProductListItem product = new()
        {
            Id = _products.Max(item => item.Id) + 1,
            Name = request.Name,
            Category = request.CategoryName,
            Price = request.Price,
            Quantity = request.Quantity,
            IsActive = request.IsActive
        };

        _products.Add(product);

        return product;
    }

    public async Task<ProductListItem> UpdateProductAsync(int productId, ProductSaveRequest request, CancellationToken cancellationToken = default)
    {
        await Task.Delay(150, cancellationToken);

        ProductListItem product = _products.FirstOrDefault(item => item.Id == productId)
            ?? throw new InvalidOperationException("Товар не знайдено.");

        product.Name = request.Name;
        product.Category = request.CategoryName;
        product.Price = request.Price;
        product.Quantity = request.Quantity;
        product.IsActive = request.IsActive;

        return product;
    }

    public async Task<ProductStock> GetProductStockAsync(int productId, CancellationToken cancellationToken = default)
    {
        await Task.Delay(150, cancellationToken);

        ProductListItem product = _products.FirstOrDefault(item => item.Id == productId)
            ?? throw new InvalidOperationException("Товар не знайдено.");

        return new ProductStock { ProductId = product.Id, Quantity = product.Quantity };
    }

    public async Task<ProductStock> UpdateProductStockAsync(int productId, int quantity, CancellationToken cancellationToken = default)
    {
        await Task.Delay(150, cancellationToken);

        if (quantity < 0)
        {
            throw new InvalidOperationException("Кількість не може бути меншою за 0.");
        }

        ProductListItem product = _products.FirstOrDefault(item => item.Id == productId)
            ?? throw new InvalidOperationException("Товар не знайдено.");

        product.Quantity = quantity;
        return new ProductStock { ProductId = product.Id, Quantity = product.Quantity };
    }

    public async Task<ReportSummary> GetOrdersReportAsync(DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        await Task.Delay(150, cancellationToken);
        return BuildReportSummary(from, to);
    }

    public async Task<(DateTime Date, int TotalOrders, decimal TotalAmount, IReadOnlyList<TodayOrderReportItem> Orders)> GetTodayOrdersReportAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        await Task.Delay(150, cancellationToken);

        List<TodayOrderReportItem> orders = _orders
            .Where(order => order.CreatedAt.Date == date.Date)
            .OrderByDescending(order => order.CreatedAt)
            .Select(order => new TodayOrderReportItem
            {
                Id = order.Id,
                OrderNumber = order.Number,
                CustomerFullName = order.Customer,
                StatusCode = order.Status,
                StatusName = GetStatusText(order.Status),
                TotalAmount = order.Total,
                CreatedAt = order.CreatedAt
            })
            .ToList();

        return (date.Date, orders.Count, orders.Sum(order => order.TotalAmount), orders);
    }

    public async Task<IReadOnlyList<ProductSalesReportItem>> GetProductSalesReportAsync(DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        await Task.Delay(150, cancellationToken);

        return _orderDetails.Values
            .Where(order => IsInDateRange(order.CreatedAt, from, to))
            .SelectMany(order => order.Items)
            .GroupBy(item => item.ProductId)
            .Select(group =>
            {
                ProductListItem? product = _products.FirstOrDefault(item => item.Id == group.Key);
                return new ProductSalesReportItem
                {
                    ProductId = group.Key,
                    ProductName = product?.Name ?? group.First().ProductName,
                    CategoryName = product?.Category ?? "-",
                    Quantity = group.Sum(item => item.Quantity),
                    TotalAmount = group.Sum(item => item.LineTotal)
                };
            })
            .OrderByDescending(item => item.TotalAmount)
            .ToList();
    }

    public async Task<IReadOnlyList<LowStockProductReportItem>> GetLowStockReportAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(150, cancellationToken);

        return _products
            .Where(product => product.IsLowStock)
            .OrderBy(product => product.Quantity)
            .Select(product => new LowStockProductReportItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                CategoryName = product.Category,
                Quantity = product.Quantity,
                Price = product.Price
            })
            .ToList();
    }

    public async Task<string> GetManagerSummaryReportAsync(DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        await Task.Delay(150, cancellationToken);

        ReportSummary report = BuildReportSummary(from, to);
        return
            $"Звіт для керівника\n" +
            $"{GetPeriodText(from, to)}\n" +
            $"Замовлень: {report.TotalOrders}\n" +
            $"Загальна сума: {report.TotalAmount:N2} грн\n" +
            $"Середній чек: {report.AverageOrderAmount:N2} грн\n" +
            $"Нові: {report.NewOrders}, в роботі: {report.OrdersInProgress}, відвантажені: {report.ShippedOrders}, рахунки: {report.InvoicedOrders}, скасовані: {report.CancelledOrders}\n" +
            $"Топ-товар: {report.TopProductText}\n" +
            $"Товарів з низьким залишком: {report.LowStockProducts}";
    }

    private static string GetNextStatus(string currentStatus, string action)
    {
        return (currentStatus, action.ToLowerInvariant()) switch
        {
            ("NewOrder", "register") => "Registered",
            ("NewOrder", "cancel") => "Cancelled",
            ("Registered", "grant") => "Granted",
            ("Registered", "cancel") => "Cancelled",
            ("Granted", "ship") => "Shipped",
            ("Granted", "cancel") => "Cancelled",
            ("Shipped", "invoice") => "Invoiced",
            _ => throw new InvalidOperationException("Дія недоступна для поточного стану замовлення.")
        };
    }

    private static string GetActionComment(string action)
    {
        return action.ToLowerInvariant() switch
        {
            "register" => "Registered for processing",
            "grant" => "Approved",
            "ship" => "Sent to customer",
            "invoice" => "Invoice issued",
            "cancel" => "Cancelled",
            _ => "Status changed"
        };
    }

    private OrderDetailItem CreateMockItem(CreateOrderItemRequest request)
    {
        ProductListItem? product = _products.FirstOrDefault(item => item.Id == request.ProductId);
        decimal price = product?.Price ?? 1m;

        return new OrderDetailItem
        {
            Id = _nextOrderItemId++,
            ProductId = request.ProductId,
            ProductName = product?.Name ?? $"Product #{request.ProductId}",
            Quantity = request.Quantity,
            UnitPrice = price,
            LineTotal = request.Quantity * price
        };
    }

    private void RecalculateOrderTotal(OrderDetail order)
    {
        order.Total = order.Items.Sum(item => item.LineTotal);

        OrderListItem? listItem = _orders.FirstOrDefault(item => item.Id == order.Id);
        if (listItem is not null)
        {
            listItem.Total = order.Total;
        }
    }

    private ReportSummary BuildReportSummary(DateTime? from = null, DateTime? to = null)
    {
        List<OrderListItem> orders = _orders
            .Where(order => IsInDateRange(order.CreatedAt, from, to))
            .ToList();

        List<OrderDetail> orderDetails = _orderDetails.Values
            .Where(order => IsInDateRange(order.CreatedAt, from, to))
            .ToList();

        ProductSalesReportItem? topProduct = orderDetails
            .SelectMany(order => order.Items)
            .GroupBy(item => item.ProductName)
            .Select(group => new ProductSalesReportItem
            {
                ProductName = group.Key,
                Quantity = group.Sum(item => item.Quantity)
            })
            .OrderByDescending(item => item.Quantity)
            .FirstOrDefault();

        return new ReportSummary
        {
            TotalOrders = orders.Count,
            NewOrders = orders.Count(order => order.Status == "NewOrder"),
            OrdersInProgress = orders.Count(order => order.Status is "Registered" or "Granted"),
            ShippedOrders = orders.Count(order => order.Status == "Shipped"),
            InvoicedOrders = orders.Count(order => order.Status == "Invoiced"),
            CancelledOrders = orders.Count(order => order.Status == "Cancelled"),
            TotalAmount = orders.Sum(order => order.Total),
            AverageOrderAmount = orders.Count == 0 ? 0 : orders.Average(order => order.Total),
            LowStockProducts = _products.Count(product => product.IsLowStock),
            TopProductName = topProduct?.ProductName ?? "-",
            TopProductQuantity = topProduct?.Quantity ?? 0
        };
    }

    private static bool IsInDateRange(DateTime date, DateTime? from, DateTime? to)
    {
        if (from.HasValue && date.Date < from.Value.Date)
        {
            return false;
        }

        if (to.HasValue && date.Date > to.Value.Date)
        {
            return false;
        }

        return true;
    }

    private static string GetPeriodText(DateTime? from, DateTime? to)
    {
        if (from.HasValue && to.HasValue)
        {
            return $"Період: {from.Value:dd.MM.yyyy} - {to.Value:dd.MM.yyyy}";
        }

        if (from.HasValue)
        {
            return $"Період: з {from.Value:dd.MM.yyyy}";
        }

        if (to.HasValue)
        {
            return $"Період: до {to.Value:dd.MM.yyyy}";
        }

        return "Період: весь час";
    }

    private static string GetStatusText(string status)
    {
        return status switch
        {
            "NewOrder" => "Нове",
            "Registered" => "Зареєстроване",
            "Granted" => "Погоджене",
            "Shipped" => "Відправлене",
            "Invoiced" => "Рахунок",
            "Cancelled" => "Скасоване",
            _ => status
        };
    }
}
