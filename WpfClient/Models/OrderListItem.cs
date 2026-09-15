namespace WpfClient.Models;

public sealed class OrderListItem
{
    public string Number { get; set; } = string.Empty;

    public string Customer { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public decimal Total { get; set; }

    public string TotalText => $"{Total:N2} грн";
}
