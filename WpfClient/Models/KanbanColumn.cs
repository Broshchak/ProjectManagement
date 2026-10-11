using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfClient.Models;

public sealed class KanbanColumn : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title => OrderStatusNames.ToText(Status);

    public string Status { get; init; } = string.Empty;

    public ObservableCollection<OrderListItem> Orders { get; } = [];

    public int Count => Orders.Count;

    public decimal Total => Orders.Sum(order => order.Total);

    public string TotalText => $"{Total:N2} грн";

    public void RefreshMetrics()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(TotalText));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
