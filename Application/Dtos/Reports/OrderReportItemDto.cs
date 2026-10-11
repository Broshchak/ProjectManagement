namespace Application.Dtos.Reports;

public sealed record OrderReportItemDto(
    int Id,
    string OrderNumber,
    string CustomerFullName,
    string StatusCode,
    string StatusName,
    decimal TotalAmount,
    DateTime CreatedAt);
