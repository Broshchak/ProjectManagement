using WpfClient.Models;

namespace WpfClient.Services;

public sealed class MockOrdersApiClient : IOrdersApiClient
{
    private readonly IReadOnlyList<OrderListItem> _orders =
    [
        new() { Number = "ORD-0001", CreatedAt = new DateTime(2026, 9, 1), Customer = "Demo Customer", Status = "NewOrder", Total = 90m },
        new() { Number = "ORD-0002", CreatedAt = new DateTime(2026, 9, 3), Customer = "Lviv Office", Status = "Registered", Total = 475m },
        new() { Number = "ORD-0003", CreatedAt = new DateTime(2026, 9, 5), Customer = "Student Lab", Status = "Granted", Total = 860m },
        new() { Number = "ORD-0004", CreatedAt = new DateTime(2026, 9, 8), Customer = "Library", Status = "Shipped", Total = 2400m },
        new() { Number = "ORD-0005", CreatedAt = new DateTime(2026, 9, 10), Customer = "Dean Office", Status = "Invoiced", Total = 725m }
    ];

    private readonly IReadOnlyDictionary<string, OrderDetail> _orderDetails = new Dictionary<string, OrderDetail>
    {
        ["ORD-0001"] = new()
        {
            Number = "ORD-0001",
            CreatedAt = new DateTime(2026, 9, 1),
            Customer = "Demo Customer",
            Status = "NewOrder",
            Total = 90m,
            Comment = "Demo order for first run",
            Items =
            [
                new() { ProductName = "Notebook A5", Quantity = 2, UnitPrice = 45m }
            ],
            StatusHistory =
            [
                new() { PreviousStatus = "-", NewStatus = "NewOrder", ChangedBy = "System Administrator", ChangedAt = new DateTime(2026, 9, 1, 9, 15, 0), Comment = "Initial status" }
            ]
        },
        ["ORD-0002"] = new()
        {
            Number = "ORD-0002",
            CreatedAt = new DateTime(2026, 9, 3),
            Customer = "Lviv Office",
            Status = "Registered",
            Total = 475m,
            Comment = "Office supplies restock",
            Items =
            [
                new() { ProductName = "Blue pen", Quantity = 10, UnitPrice = 12.5m },
                new() { ProductName = "Folder", Quantity = 10, UnitPrice = 18m },
                new() { ProductName = "Notebook A5", Quantity = 4, UnitPrice = 42.5m }
            ],
            StatusHistory =
            [
                new() { PreviousStatus = "-", NewStatus = "NewOrder", ChangedBy = "Order Manager", ChangedAt = new DateTime(2026, 9, 3, 10, 0, 0), Comment = "Created" },
                new() { PreviousStatus = "NewOrder", NewStatus = "Registered", ChangedBy = "Order Manager", ChangedAt = new DateTime(2026, 9, 3, 10, 30, 0), Comment = "Registered for processing" }
            ]
        },
        ["ORD-0003"] = new()
        {
            Number = "ORD-0003",
            CreatedAt = new DateTime(2026, 9, 5),
            Customer = "Student Lab",
            Status = "Granted",
            Total = 860m,
            Comment = "Equipment for lab class",
            Items =
            [
                new() { ProductName = "USB flash drive 32GB", Quantity = 2, UnitPrice = 220m },
                new() { ProductName = "Wireless mouse", Quantity = 1, UnitPrice = 420m }
            ],
            StatusHistory =
            [
                new() { PreviousStatus = "-", NewStatus = "NewOrder", ChangedBy = "Order Manager", ChangedAt = new DateTime(2026, 9, 5, 8, 45, 0), Comment = "Created" },
                new() { PreviousStatus = "NewOrder", NewStatus = "Registered", ChangedBy = "Order Manager", ChangedAt = new DateTime(2026, 9, 5, 9, 5, 0), Comment = "Registered" },
                new() { PreviousStatus = "Registered", NewStatus = "Granted", ChangedBy = "Company Director", ChangedAt = new DateTime(2026, 9, 5, 11, 20, 0), Comment = "Approved" }
            ]
        },
        ["ORD-0004"] = new()
        {
            Number = "ORD-0004",
            CreatedAt = new DateTime(2026, 9, 8),
            Customer = "Library",
            Status = "Shipped",
            Total = 2400m,
            Comment = "Furniture delivery",
            Items =
            [
                new() { ProductName = "Office chair", Quantity = 1, UnitPrice = 2400m }
            ],
            StatusHistory =
            [
                new() { PreviousStatus = "-", NewStatus = "NewOrder", ChangedBy = "Order Manager", ChangedAt = new DateTime(2026, 9, 8, 12, 10, 0), Comment = "Created" },
                new() { PreviousStatus = "NewOrder", NewStatus = "Registered", ChangedBy = "Order Manager", ChangedAt = new DateTime(2026, 9, 8, 12, 25, 0), Comment = "Registered" },
                new() { PreviousStatus = "Registered", NewStatus = "Granted", ChangedBy = "Company Director", ChangedAt = new DateTime(2026, 9, 8, 13, 0, 0), Comment = "Approved" },
                new() { PreviousStatus = "Granted", NewStatus = "Shipped", ChangedBy = "Order Manager", ChangedAt = new DateTime(2026, 9, 9, 9, 0, 0), Comment = "Sent to customer" }
            ]
        },
        ["ORD-0005"] = new()
        {
            Number = "ORD-0005",
            CreatedAt = new DateTime(2026, 9, 10),
            Customer = "Dean Office",
            Status = "Invoiced",
            Total = 725m,
            Comment = "Monthly supplies",
            Items =
            [
                new() { ProductName = "Folder", Quantity = 5, UnitPrice = 18m },
                new() { ProductName = "Notebook A5", Quantity = 5, UnitPrice = 45m },
                new() { ProductName = "Wireless mouse", Quantity = 1, UnitPrice = 410m }
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

    private readonly IReadOnlyList<ProductListItem> _products =
    [
        new() { Name = "Notebook A5", Category = "Office supplies", Price = 45m, Quantity = 120, IsActive = true },
        new() { Name = "Blue pen", Category = "Office supplies", Price = 12.5m, Quantity = 300, IsActive = true },
        new() { Name = "Folder", Category = "Office supplies", Price = 18m, Quantity = 80, IsActive = true },
        new() { Name = "USB flash drive 32GB", Category = "Electronics", Price = 220m, Quantity = 35, IsActive = true },
        new() { Name = "Wireless mouse", Category = "Electronics", Price = 420m, Quantity = 25, IsActive = true },
        new() { Name = "Office chair", Category = "Furniture", Price = 2400m, Quantity = 10, IsActive = false }
    ];

    public async Task<IReadOnlyList<OrderListItem>> GetOrdersAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(150, cancellationToken);
        return _orders;
    }

    public async Task<OrderDetail?> GetOrderDetailsAsync(string orderNumber, CancellationToken cancellationToken = default)
    {
        await Task.Delay(150, cancellationToken);
        return _orderDetails.GetValueOrDefault(orderNumber);
    }

    public async Task<IReadOnlyList<ProductListItem>> GetProductsAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(150, cancellationToken);
        return _products;
    }

    public async Task<ReportSummary> GetReportSummaryAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(150, cancellationToken);

        return new ReportSummary
        {
            NewOrders = _orders.Count(order => order.Status == "NewOrder"),
            OrdersInProgress = _orders.Count(order => order.Status is "Registered" or "Granted"),
            ShippedOrders = _orders.Count(order => order.Status == "Shipped"),
            InvoicedOrders = _orders.Count(order => order.Status == "Invoiced"),
            TotalAmount = _orders.Sum(order => order.Total)
        };
    }
}
