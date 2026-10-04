using System.ComponentModel.DataAnnotations;

namespace Application.Dtos.Orders
{
    public record CreateOrderDto(
        [Required, StringLength(40)] string OrderNumber,
        [Range(1, int.MaxValue)] int CustomerId,
        string? Comment,
        [Required, MinLength(1)] IReadOnlyCollection<CreateOrderItemDto> Items
    );
}
