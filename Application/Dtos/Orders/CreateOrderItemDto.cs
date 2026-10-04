using System.ComponentModel.DataAnnotations;

namespace Application.Dtos.Orders
{
    public record CreateOrderItemDto(
        [Range(1, int.MaxValue)] int ProductId,
        [Range(1, int.MaxValue)] int Quantity
    );
}
