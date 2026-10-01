namespace Application.Dtos.Orders
{
    public record OrderItemDto(
        int Id,
        int ProductId,
        string ProductName,
        int Quantity,
        decimal UnitPrice,
        string CurrencyCode,
        decimal LineTotal
    );
}
