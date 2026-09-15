using WpfClient.Models;

namespace WpfClient.Services;

public sealed class MockOrdersApiClient : IOrdersApiClient
{
    private readonly IReadOnlyList<OrderListItem> _orders =
    [
        new() { Number = "ORD-0001", Customer = "Demo Customer", Status = "NewOrder", Total = 90m },
        new() { Number = "ORD-0002", Customer = "Lviv Office", Status = "Registered", Total = 475m },
        new() { Number = "ORD-0003", Customer = "Student Lab", Status = "Granted", Total = 860m },
        new() { Number = "ORD-0004", Customer = "Library", Status = "Shipped", Total = 2400m },
        new() { Number = "ORD-0005", Customer = "Dean Office", Status = "Invoiced", Total = 725m }
    ];

    private readonly IReadOnlyList<ProductListItem> _products =
    [
        new() { Name = "Notebook A5", Category = "Office supplies", Price = 45m, Quantity = 120 },
        new() { Name = "Blue pen", Category = "Office supplies", Price = 12.5m, Quantity = 300 },
        new() { Name = "Folder", Category = "Office supplies", Price = 18m, Quantity = 80 },
        new() { Name = "USB flash drive 32GB", Category = "Electronics", Price = 220m, Quantity = 35 },
        new() { Name = "Wireless mouse", Category = "Electronics", Price = 420m, Quantity = 25 },
        new() { Name = "Office chair", Category = "Furniture", Price = 2400m, Quantity = 10 }
    ];

    public async Task<IReadOnlyList<OrderListItem>> GetOrdersAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(150, cancellationToken);
        return _orders;
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
