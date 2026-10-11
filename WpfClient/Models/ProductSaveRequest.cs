namespace WpfClient.Models;

public sealed class ProductSaveRequest
{
    public string Name { get; set; } = string.Empty;

    public string CategoryName { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public int Quantity { get; set; }

    public bool IsActive { get; set; }
}
