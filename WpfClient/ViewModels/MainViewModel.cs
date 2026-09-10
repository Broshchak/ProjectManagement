using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Input;
using WpfClient.Views;

namespace WpfClient.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private NavigationItem _selectedItem;
    private UserControl _currentView;

    public MainViewModel()
    {
        NavigationItems = new ObservableCollection<NavigationItem>
        {
            new("Dashboard", "Огляд", new DashboardView()),
            new("Orders", "Замовлення", new OrdersView()),
            new("Products", "Товари", new ProductsView()),
            new("Users", "Користувачі", new UsersView()),
            new("Reports", "Звіти", new ReportsView())
        };

        NavigateCommand = new RelayCommand(item =>
        {
            if (item is NavigationItem navigationItem)
            {
                SelectedItem = navigationItem;
            }
        });

        _selectedItem = NavigationItems[0];
        _currentView = _selectedItem.View;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<NavigationItem> NavigationItems { get; }

    public ICommand NavigateCommand { get; }

    public NavigationItem SelectedItem
    {
        get => _selectedItem;
        private set
        {
            if (_selectedItem == value)
            {
                return;
            }

            _selectedItem = value;
            CurrentView = value.View;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CurrentTitle));
        }
    }

    public UserControl CurrentView
    {
        get => _currentView;
        private set
        {
            if (_currentView == value)
            {
                return;
            }

            _currentView = value;
            OnPropertyChanged();
        }
    }

    public string CurrentTitle => SelectedItem.Title;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
