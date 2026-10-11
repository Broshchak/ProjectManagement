namespace Application.Dtos.Reports;

public sealed record ProductSalesReportItemDto(
    int ProductId,
    string ProductName,
    string CategoryName,
    int Quantity,
    decimal TotalAmount);
