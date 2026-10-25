namespace Application.Dtos.Reports;

public sealed record OrderReportDto(
    int TotalOrders,
    int NewOrders,
    int OrdersInProgress,
    int ShippedOrders,
    int InvoicedOrders,
    int CancelledOrders,
    decimal TotalAmount,
    decimal AverageOrderAmount,
    int LowStockProducts,
    string TopProductName,
    int TopProductQuantity);

public sealed record TodayOrdersReportDto(
    DateTime Date,
    int TotalOrders,
    decimal TotalAmount,
    IReadOnlyCollection<OrderReportItemDto> Orders);

public sealed record OrderReportItemDto(
    int Id,
    string OrderNumber,
    string CustomerFullName,
    string StatusCode,
    string StatusName,
    decimal TotalAmount,
    DateTime CreatedAt);

public sealed record ProductSalesReportItemDto(
    int ProductId,
    string ProductName,
    string CategoryName,
    int Quantity,
    decimal TotalAmount);

public sealed record LowStockProductReportItemDto(
    int ProductId,
    string ProductName,
    string CategoryName,
    int Quantity,
    decimal Price);

public sealed record ManagerSummaryReportDto(
    DateTime GeneratedAt,
    string Text);
