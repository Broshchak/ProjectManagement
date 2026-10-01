namespace WpfClient.Models;

public sealed class OrderListItem
{
    public int Id { get; set; }

    public int CustomerId { get; set; }

    public string Number { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public string Customer { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public decimal Total { get; set; }

    public string CreatedAtText => CreatedAt.ToString("dd.MM.yyyy");

    public string TotalText => $"{Total:N2} грн";
}
