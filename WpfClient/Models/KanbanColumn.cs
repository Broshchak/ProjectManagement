using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfClient.Models;

public sealed class KanbanColumn : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title => OrderStatusNames.ToText(Status, Name);
    public string? Name { get; init; }
    public string Color { get; init; } = "#255C99";

    public string Status { get; init; } = string.Empty;

    private bool _isDropTarget;
    public bool IsDropTarget
    {
        get => _isDropTarget;
        set
        {
            if (_isDropTarget == value) return;
            _isDropTarget = value;
            OnPropertyChanged();
        }
    }

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
