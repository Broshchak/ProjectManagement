using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfClient.Models;

public sealed class ProductListItem : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _category = string.Empty;
    private decimal _price;
    private int _quantity;
    private bool _isActive = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name
    {
        get => _name;
        set
        {
            if (_name == value)
            {
                return;
            }

            _name = value;
            OnPropertyChanged();
        }
    }

    public string Category
    {
        get => _category;
        set
        {
            if (_category == value)
            {
                return;
            }

            _category = value;
            OnPropertyChanged();
        }
    }

    public decimal Price
    {
        get => _price;
        set
        {
            if (_price == value)
            {
                return;
            }

            _price = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PriceText));
        }
    }

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
            OnPropertyChanged();
        }
    }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value)
            {
                return;
            }

            _isActive = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ActiveText));
        }
    }

    public string PriceText => $"{Price:N2} грн";

    public string ActiveText => IsActive ? "Активний" : "Неактивний";

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
