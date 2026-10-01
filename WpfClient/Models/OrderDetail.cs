using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfClient.Models;

public sealed class OrderDetail
{
    public int Id { get; set; }

    public string Number { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public string Customer { get; set; } = string.Empty;

    public int CustomerId { get; set; }

    public string Status { get; set; } = string.Empty;

    public decimal Total { get; set; }

    public string? Comment { get; set; }

    public List<OrderDetailItem> Items { get; set; } = [];

    public List<OrderStatusHistoryItem> StatusHistory { get; set; } = [];

    public string CreatedAtText => CreatedAt.ToString("dd.MM.yyyy");

    public string TotalText => $"{Total:N2} грн";
}

public sealed class OrderDetailItem : INotifyPropertyChanged
{
    private int _quantity;

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

    public string UnitPriceText => $"{UnitPrice:N2} грн";

    public string LineTotalText => $"{LineTotal:N2} грн";

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class OrderStatusHistoryItem
{
    public string PreviousStatus { get; set; } = string.Empty;

    public string NewStatus { get; set; } = string.Empty;

    public string ChangedBy { get; set; } = string.Empty;

    public DateTime ChangedAt { get; set; }

    public string? Comment { get; set; }

    public string ChangedAtText => ChangedAt.ToString("dd.MM.yyyy HH:mm");
}
