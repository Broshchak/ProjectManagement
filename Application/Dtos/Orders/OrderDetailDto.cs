namespace Application.Dtos.Orders
{
    public record OrderDetailDto(
        int Id,
        string OrderNumber,
        string StatusCode,
        string StatusName,
        bool IsStatusFinal,
        int CustomerId,
        string CustomerFullName,
        string? CustomerPhone,
        string? CustomerEmail,
        string? CustomerAddress,
        int CreatedByUserId,
        decimal TotalAmount,
        string CurrencyCode,
        string? Comment,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        IReadOnlyCollection<OrderItemDto> Items,
        IReadOnlyCollection<OrderStatusHistoryDto> StatusHistory
    );
}
