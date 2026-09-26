using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Input;
using WpfClient.Models;
using WpfClient.Services;
using WpfClient.Views;

namespace WpfClient.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly IOrdersApiClient _apiClient;
    private readonly ApiClientSettings _apiSettings;
    private readonly IReadOnlyList<NavigationItem> _allNavigationItems;
    private NavigationItem _selectedItem;
    private UserControl _currentView;
    private TestUser _selectedLoginUser;
    private ProductListItem? _selectedProduct;
    private ReportSummary _summary = new();
    private bool _isAuthenticated;
    private bool _isLoading;
    private bool _isProductEditorOpen;
    private bool _isEditingProduct;
    private string? _loginError;
    private string? _apiErrorMessage;
    private string? _productEditorError;
    private string? _currentUserName;
    private string? _currentRoleName;
    private string _productName = string.Empty;
    private string _productCategory = string.Empty;
    private decimal _productPrice;
    private int _productQuantity;
    private bool _productIsActive = true;

    public MainViewModel()
    {
        _apiSettings = ApiClientSettings.Load();
        _apiClient = OrdersApiClientFactory.Create(_apiSettings);

        TestUsers = new ObservableCollection<TestUser>
        {
            new("admin", "System Administrator", "Admin", "admin"),
            new("manager", "Order Manager", "Manager", "manager"),
            new("viewer", "Read Only User", "Viewer", "viewer"),
            new("director", "Company Director", "Director", "director")
        };

        _allNavigationItems = new[]
        {
            new NavigationItem("Dashboard", "Огляд", new DashboardView(), new[] { "Admin", "Viewer", "Director" }),
            new NavigationItem("Orders", "Замовлення", new OrdersView(), new[] { "Admin", "Manager", "Viewer", "Director" }),
            new NavigationItem("Products", "Товари", new ProductsView(), new[] { "Admin" }),
            new NavigationItem("Users", "Користувачі", new UsersView(), new[] { "Admin", "Director" }),
            new NavigationItem("Reports", "Звіти", new ReportsView(), new[] { "Admin", "Viewer", "Director" })
        };

        NavigationItems = new ObservableCollection<NavigationItem>();

        NavigateCommand = new RelayCommand(item =>
        {
            if (item is NavigationItem navigationItem)
            {
                SelectedItem = navigationItem;
            }
        });

        SignOutCommand = new RelayCommand(_ => SignOut(), _ => IsAuthenticated);
        RefreshDataCommand = new RelayCommand(async _ => await LoadClientDataAsync(), _ => IsAuthenticated && !IsLoading);
        BeginAddProductCommand = new RelayCommand(_ => BeginAddProduct(), _ => CanManageProducts);
        BeginEditProductCommand = new RelayCommand(_ => BeginEditProduct(), _ => CanManageProducts && SelectedProduct is not null);
        SaveProductCommand = new RelayCommand(_ => SaveProduct(), _ => CanManageProducts && IsProductEditorOpen);
        CancelProductEditCommand = new RelayCommand(_ => CloseProductEditor(), _ => IsProductEditorOpen);

        _selectedLoginUser = TestUsers[0];
        _selectedItem = _allNavigationItems[0];
        _currentView = _selectedItem.View;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<NavigationItem> NavigationItems { get; }

    public ObservableCollection<OrderListItem> Orders { get; } = [];

    public ObservableCollection<ProductListItem> Products { get; } = [];

    public ObservableCollection<TestUser> TestUsers { get; }

    public ICommand NavigateCommand { get; }

    public ICommand SignOutCommand { get; }

    public ICommand RefreshDataCommand { get; }

    public ICommand BeginAddProductCommand { get; }

    public ICommand BeginEditProductCommand { get; }

    public ICommand SaveProductCommand { get; }

    public ICommand CancelProductEditCommand { get; }

    public TestUser SelectedLoginUser
    {
        get => _selectedLoginUser;
        set
        {
            if (_selectedLoginUser == value)
            {
                return;
            }

            _selectedLoginUser = value;
            LoginError = null;
            OnPropertyChanged();
        }
    }

    public bool IsAuthenticated
    {
        get => _isAuthenticated;
        private set
        {
            if (_isAuthenticated == value)
            {
                return;
            }

            _isAuthenticated = value;
            OnPropertyChanged();
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (_isLoading == value)
            {
                return;
            }

            _isLoading = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LoadingStatusText));
        }
    }

    public string? LoginError
    {
        get => _loginError;
        private set
        {
            if (_loginError == value)
            {
                return;
            }

            _loginError = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasLoginError));
        }
    }

    public bool HasLoginError => !string.IsNullOrWhiteSpace(LoginError);

    public string? ApiErrorMessage
    {
        get => _apiErrorMessage;
        private set
        {
            if (_apiErrorMessage == value)
            {
                return;
            }

            _apiErrorMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasApiError));
        }
    }

    public bool HasApiError => !string.IsNullOrWhiteSpace(ApiErrorMessage);

    public string ApiModeText => _apiSettings.UseMockData
        ? "Mock-дані"
        : $"API: {_apiSettings.BaseUrl}";

    public string LoadingStatusText => IsLoading
        ? "Завантаження даних..."
        : ApiModeText;

    public ReportSummary Summary
    {
        get => _summary;
        private set
        {
            _summary = value;
            OnPropertyChanged();
        }
    }

    public ProductListItem? SelectedProduct
    {
        get => _selectedProduct;
        set
        {
            if (_selectedProduct == value)
            {
                return;
            }

            _selectedProduct = value;
            OnPropertyChanged();
        }
    }

    public bool IsProductEditorOpen
    {
        get => _isProductEditorOpen;
        private set
        {
            if (_isProductEditorOpen == value)
            {
                return;
            }

            _isProductEditorOpen = value;
            OnPropertyChanged();
        }
    }

    public string ProductEditorTitle => _isEditingProduct ? "Редагування товару" : "Новий товар";

    public string? ProductEditorError
    {
        get => _productEditorError;
        private set
        {
            if (_productEditorError == value)
            {
                return;
            }

            _productEditorError = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasProductEditorError));
        }
    }

    public bool HasProductEditorError => !string.IsNullOrWhiteSpace(ProductEditorError);

    public string ProductName
    {
        get => _productName;
        set
        {
            if (_productName == value)
            {
                return;
            }

            _productName = value;
            OnPropertyChanged();
        }
    }

    public string ProductCategory
    {
        get => _productCategory;
        set
        {
            if (_productCategory == value)
            {
                return;
            }

            _productCategory = value;
            OnPropertyChanged();
        }
    }

    public decimal ProductPrice
    {
        get => _productPrice;
        set
        {
            if (_productPrice == value)
            {
                return;
            }

            _productPrice = value;
            OnPropertyChanged();
        }
    }

    public int ProductQuantity
    {
        get => _productQuantity;
        set
        {
            if (_productQuantity == value)
            {
                return;
            }

            _productQuantity = value;
            OnPropertyChanged();
        }
    }

    public bool ProductIsActive
    {
        get => _productIsActive;
        set
        {
            if (_productIsActive == value)
            {
                return;
            }

            _productIsActive = value;
            OnPropertyChanged();
        }
    }

    public string? CurrentUserName
    {
        get => _currentUserName;
        private set
        {
            if (_currentUserName == value)
            {
                return;
            }

            _currentUserName = value;
            OnPropertyChanged();
        }
    }

    public string? CurrentRoleName
    {
        get => _currentRoleName;
        private set
        {
            if (_currentRoleName == value)
            {
                return;
            }

            _currentRoleName = value;
            OnPropertyChanged();
        }
    }

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

    public bool CanManageOrders => CurrentRoleName is "Admin" or "Manager" or "Director";

    public bool CanManageProducts => CurrentRoleName is "Admin";

    public bool CanManageUsers => CurrentRoleName is "Admin" or "Director";

    public bool CanViewReports => CurrentRoleName is "Admin" or "Viewer" or "Director";

    public bool SignIn(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            LoginError = "Введіть пароль для тестового користувача.";
            return false;
        }

        if (password != SelectedLoginUser.Password)
        {
            LoginError = "Неправильний пароль для вибраного тестового користувача.";
            return false;
        }

        CurrentUserName = SelectedLoginUser.FullName;
        CurrentRoleName = SelectedLoginUser.RoleName;
        LoginError = null;
        IsAuthenticated = true;
        RefreshNavigationItems();
        SelectedItem = NavigationItems[0];
        OnPermissionPropertiesChanged();
        _ = LoadClientDataAsync();

        return true;
    }

    private void SignOut()
    {
        IsAuthenticated = false;
        CurrentUserName = null;
        CurrentRoleName = null;
        LoginError = null;
        ApiErrorMessage = null;
        Orders.Clear();
        Products.Clear();
        SelectedProduct = null;
        CloseProductEditor();
        Summary = new ReportSummary();
        NavigationItems.Clear();
        SelectedItem = _allNavigationItems[0];
        OnPermissionPropertiesChanged();
    }

    private void RefreshNavigationItems()
    {
        NavigationItems.Clear();

        foreach (NavigationItem item in _allNavigationItems.Where(item => item.IsAllowedFor(CurrentRoleName ?? string.Empty)))
        {
            NavigationItems.Add(item);
        }
    }

    private void OnPermissionPropertiesChanged()
    {
        OnPropertyChanged(nameof(CanManageOrders));
        OnPropertyChanged(nameof(CanManageProducts));
        OnPropertyChanged(nameof(CanManageUsers));
        OnPropertyChanged(nameof(CanViewReports));
    }

    private async Task LoadClientDataAsync()
    {
        IsLoading = true;
        ApiErrorMessage = null;

        try
        {
            IReadOnlyList<OrderListItem> orders = await _apiClient.GetOrdersAsync();
            IReadOnlyList<ProductListItem> products = await _apiClient.GetProductsAsync();
            ReportSummary summary = await _apiClient.GetReportSummaryAsync();

            ReplaceCollection(Orders, orders);
            ReplaceCollection(Products, products);
            SelectedProduct = Products.FirstOrDefault();
            Summary = summary;
        }
        catch (Exception exception)
        {
            ApiErrorMessage = $"Не вдалося отримати дані. Перевірте підключення до API або увімкніть mock-дані. Деталі: {exception.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();

        foreach (T item in source)
        {
            target.Add(item);
        }
    }

    private void BeginAddProduct()
    {
        _isEditingProduct = false;
        ProductName = string.Empty;
        ProductCategory = string.Empty;
        ProductPrice = 1m;
        ProductQuantity = 0;
        ProductIsActive = true;
        ProductEditorError = null;
        IsProductEditorOpen = true;
        OnPropertyChanged(nameof(ProductEditorTitle));
    }

    private void BeginEditProduct()
    {
        if (SelectedProduct is null)
        {
            ProductEditorError = "Оберіть товар для редагування.";
            return;
        }

        _isEditingProduct = true;
        ProductName = SelectedProduct.Name;
        ProductCategory = SelectedProduct.Category;
        ProductPrice = SelectedProduct.Price;
        ProductQuantity = SelectedProduct.Quantity;
        ProductIsActive = SelectedProduct.IsActive;
        ProductEditorError = null;
        IsProductEditorOpen = true;
        OnPropertyChanged(nameof(ProductEditorTitle));
    }

    private void SaveProduct()
    {
        if (!ValidateProductForm())
        {
            return;
        }

        if (_isEditingProduct && SelectedProduct is not null)
        {
            SelectedProduct.Name = ProductName.Trim();
            SelectedProduct.Category = ProductCategory.Trim();
            SelectedProduct.Price = ProductPrice;
            SelectedProduct.Quantity = ProductQuantity;
            SelectedProduct.IsActive = ProductIsActive;
        }
        else
        {
            ProductListItem product = new()
            {
                Name = ProductName.Trim(),
                Category = ProductCategory.Trim(),
                Price = ProductPrice,
                Quantity = ProductQuantity,
                IsActive = ProductIsActive
            };

            Products.Add(product);
            SelectedProduct = product;
        }

        CloseProductEditor();
    }

    private bool ValidateProductForm()
    {
        if (string.IsNullOrWhiteSpace(ProductName))
        {
            ProductEditorError = "Вкажіть назву товару.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(ProductCategory))
        {
            ProductEditorError = "Вкажіть категорію товару.";
            return false;
        }

        if (ProductPrice <= 0)
        {
            ProductEditorError = "Ціна товару має бути більшою за 0.";
            return false;
        }

        if (ProductQuantity < 0)
        {
            ProductEditorError = "Кількість не може бути меншою за 0.";
            return false;
        }

        ProductEditorError = null;
        return true;
    }

    private void CloseProductEditor()
    {
        ProductEditorError = null;
        IsProductEditorOpen = false;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
