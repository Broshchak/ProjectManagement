namespace Application.Dtos.Orders
{
    public record CreateOrderDto(
        string OrderNumber,
        int CustomerId,
        string? Comment,
        IReadOnlyCollection<CreateOrderItemDto> Items
    );
}
