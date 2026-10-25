namespace Application.Dtos.Reports;

public sealed record LowStockProductReportItemDto(
    int ProductId,
    string ProductName,
    string CategoryName,
    int Quantity,
    decimal Price);
