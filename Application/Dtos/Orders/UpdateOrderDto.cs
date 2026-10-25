using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;

namespace Application.Dtos.Orders;

public sealed record UpdateOrderDto(
    [Required, StringLength(64, MinimumLength = 64)] string ExpectedVersion,
    string? Comment,
    [Required, MinLength(1)] IReadOnlyList<UpdateOrderItemDto> Items);

public sealed record UpdateOrderItemDto(
    [Range(0, int.MaxValue)] int Id,
    [Range(1, int.MaxValue)] int ProductId,
    [Range(1, int.MaxValue)] int Quantity);

public static class OrderVersion
{
    // A content revision also detects writes through older item endpoints, without a schema migration.
    public static string Calculate(OrderDetailDto order) => Convert.ToHexString(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            order.Id, order.StatusCode, order.IsStatusFinal, order.Comment, order.UpdatedAt,
            Items = order.Items.OrderBy(i => i.Id).Select(i => new { i.Id, i.ProductId, i.Quantity, i.UnitPrice })
        })));
}
