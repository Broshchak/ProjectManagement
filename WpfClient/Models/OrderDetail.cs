using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfClient.Models;

public sealed class OrderDetail
{
    public string Version { get; set; } = "";
    public int Id { get; set; }

    public string Number { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public string Customer { get; set; } = string.Empty;

    public int CustomerId { get; set; }

    public bool IsStatusFinal { get; set; }

    public string CurrencyCode { get; set; } = "UAH";

    public string Status { get; set; } = string.Empty;

    public string StatusText => OrderStatusNames.ToText(Status);

    public decimal Total { get; set; }

    public string? CustomerPhone { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerAddress { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public string CustomerContactText => string.Join(Environment.NewLine,
        new[] { CustomerPhone, CustomerEmail, CustomerAddress }.Where(value => !string.IsNullOrWhiteSpace(value)));

    public string? Comment { get; set; }

    public List<OrderDetailItem> Items { get; set; } = [];

    public List<OrderStatusHistoryItem> StatusHistory { get; set; } = [];

    public string CreatedAtText => CreatedAt.ToString("dd.MM.yyyy");

    public string TotalText => $"{Total:N2} {CurrencyCode}";
}

public sealed class OrderDetailItem : INotifyPropertyChanged
{
    private int _quantity;
    public bool IsQuantityReadOnly { get; set; }
    public string CurrencyCode { get; set; } = "UAH";

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Id { get; set; }

    public int ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public int Quantity
    {
        get => _quantity;
        set
        {
            if (_quantity == value)
            {
                return;
            }

            _quantity = value;
            LineTotal = value * UnitPrice;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LineTotalText));
        }
    }

    public decimal UnitPrice { get; set; }

    public decimal LineTotal { get; set; }

    public string UnitPriceText => $"{UnitPrice:N2} {CurrencyCode}";

    public string LineTotalText => $"{LineTotal:N2} {CurrencyCode}";

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class OrderStatusHistoryItem
{
    public string PreviousStatus { get; set; } = string.Empty;

    public string PreviousStatusText => OrderStatusNames.ToText(PreviousStatus);

    public string NewStatus { get; set; } = string.Empty;

    public string NewStatusText => OrderStatusNames.ToText(NewStatus);

    public string ChangedBy { get; set; } = string.Empty;

    public DateTime ChangedAt { get; set; }

    public string? Comment { get; set; }

    public string ChangedAtText => ChangedAt.ToString("dd.MM.yyyy HH:mm");
}
