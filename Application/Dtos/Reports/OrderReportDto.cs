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
