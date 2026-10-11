namespace Application.Dtos.Reports;

public sealed record TodayOrdersReportDto(
    DateTime Date,
    int TotalOrders,
    decimal TotalAmount,
    IReadOnlyCollection<OrderReportItemDto> Orders);
