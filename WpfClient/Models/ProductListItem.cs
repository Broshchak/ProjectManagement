namespace WpfClient.Models;

public sealed class ProductListItem
{
    public string Name { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public int Quantity { get; set; }

    public string PriceText => $"{Price:N2} грн";
}
