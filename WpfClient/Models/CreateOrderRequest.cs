namespace WpfClient.Models;

public sealed class CreateOrderRequest
{
    public string OrderNumber { get; set; } = string.Empty;

    public int CustomerId { get; set; }

    public string? Comment { get; set; }

    public List<CreateOrderItemRequest> Items { get; set; } = [];
}

