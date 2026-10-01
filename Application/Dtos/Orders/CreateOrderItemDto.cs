namespace Application.Dtos.Orders
{
    public record CreateOrderItemDto(
        int ProductId,
        int Quantity
    );
}
