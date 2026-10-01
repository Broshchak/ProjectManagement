namespace Application.Dtos.Orders
{
    public record OrderSummaryDto(
        int Id,
        string OrderNumber,
        string StatusCode,
        string StatusName,
        bool IsStatusFinal,
        int CustomerId,
        string CustomerFullName,
        decimal TotalAmount,
        string CurrencyCode,
        string? Comment,
        DateTime CreatedAt
    );
}
