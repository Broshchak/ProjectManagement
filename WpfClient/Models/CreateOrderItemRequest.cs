namespace WpfClient.Models;

public sealed class CreateOrderItemRequest
{
    public int ProductId { get; set; }

    public int Quantity { get; set; }
}
