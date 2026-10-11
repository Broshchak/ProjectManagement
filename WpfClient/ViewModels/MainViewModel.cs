using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
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
    private readonly List<OrderListItem> _allOrders = [];
    private readonly List<ProductListItem> _allProducts = [];
    private readonly List<OrderDetail> _loadedOrderDetails = [];
    private NavigationItem _selectedItem;
    private UserControl _currentView;
    private TestUser _selectedLoginUser;
    private TestUser? _selectedUser;
    private OrderListItem? _selectedOrder;
    private OrderDetail? _selectedOrderDetails;
    private OrderDetailItem? _selectedOrderItem;
    private CustomerListItem? _selectedOrderCustomer;
    private ProductListItem? _selectedOrderItemProduct;
    private ProductListItem? _selectedProduct;
    private ReportSummary _summary = new();
    private bool _isAuthenticated;
    private bool _isLoading;
    private bool _isOrderDetailsLoading;
    private bool _isOrderStatusActionRunning;
    private bool _isCreatingOrder;
    private bool _isProductEditorOpen;
    private bool _isEditingProduct;
    private bool _isUserEditorOpen;
    private bool _isEditingUser;
    private string? _loginError;
    private string? _apiErrorMessage;
    private string? _orderItemEditorError;
    private string? _productEditorError;
    private string? _userEditorError;
    private string? _currentUserName;
    private string? _currentRoleName;
    private string _orderSearchText = string.Empty;
    private string _orderStatusFilter = "Усі";
    private string _productSearchText = string.Empty;
    private bool _showOnlyLowStockProducts;
    private string _userLogin = string.Empty;
    private string _userFullName = string.Empty;
    private string _userRoleName = "Viewer";
    private string _userPassword = string.Empty;
    private bool _userIsActive = true;
    private bool _userTelegramAllowed;
    private string _userTelegramUserId = string.Empty;
    private string _orderNumber = string.Empty;
    private string _orderComment = string.Empty;
    private int _orderItemQuantity = 1;
    private string _productName = string.Empty;
    private string _productCategory = string.Empty;
    private decimal _productPrice;
    private int _productQuantity;
    private bool _productIsActive = true;
    private LanguageOption _selectedLanguage;

    public MainViewModel()
    {
        _apiSettings = ApiClientSettings.Load();
        _apiClient = OrdersApiClientFactory.Create(_apiSettings);
        _selectedLanguage = LanguageOptions[0];
        T.PropertyChanged += (_, _) => RefreshLanguageText();

        TestUsers = new ObservableCollection<TestUser>
        {
            new("admin", "System Administrator", "Admin", "admin"),
            new("manager", "Order Manager", "Manager", "manager"),
            new("viewer", "Read Only User", "Viewer", "viewer"),
            new("director", "Company Director", "Director", "director")
        };

        _allNavigationItems = new[]
        {
            new NavigationItem("Dashboard", "NavDashboard", new DashboardView(), new[] { "Admin", "Viewer", "Director" }),
            new NavigationItem("Orders", "NavOrders", new OrdersView(), new[] { "Admin", "Manager", "Viewer", "Director" }),
            new NavigationItem("Kanban", "NavKanban", new KanbanView(), new[] { "Admin", "Manager", "Viewer", "Director" }),
            new NavigationItem("Products", "NavProducts", new ProductsView(), new[] { "Admin", "Viewer", "Director" }),
            new NavigationItem("Users", "NavUsers", new UsersView(), new[] { "Admin", "Director" }),
            new NavigationItem("Reports", "NavReports", new ReportsView(), new[] { "Admin", "Viewer", "Director" })
        };

        NavigationItems = new ObservableCollection<NavigationItem>();
        EditableOrderItems.CollectionChanged += EditableOrderItems_CollectionChanged;

        NavigateCommand = new RelayCommand(item =>
        {
            if (item is NavigationItem navigationItem)
            {
                SelectedItem = navigationItem;
            }
        });

        SignOutCommand = new RelayCommand(_ => SignOut(), _ => IsAuthenticated);
        RefreshDataCommand = new RelayCommand(async _ => await LoadClientDataAsync(), _ => IsAuthenticated && !IsLoading);
        ClearOrderFiltersCommand = new RelayCommand(_ => ClearOrderFilters(), _ => HasOrderFilters);
        ClearProductFiltersCommand = new RelayCommand(_ => ClearProductFilters(), _ => HasProductFilters);
        OpenProductPriceSearchCommand = new RelayCommand(_ => OpenProductPriceSearch(), _ => SelectedProduct is not null);
        OpenLowStockProductsCommand = new RelayCommand(_ => OpenLowStockProducts(), _ => HasLowStockProducts);
        ChangeOrderStatusCommand = new RelayCommand(
            async action => await ChangeOrderStatusAsync(action as string),
            action => CanExecuteOrderStatusAction(action as string));
        BeginCreateOrderCommand = new RelayCommand(_ => BeginCreateOrder(), _ => CanManageOrders);
        OpenOrderEditorCommand = new RelayCommand(_ => OpenOrderEditor(), _ => CanEditSelectedOrder);
        OpenSelectOrderCustomerCommand = new RelayCommand(_ => OpenSelectOrderCustomerDialog(), _ => IsCreatingOrder && Customers.Count > 0);
        OpenAddOrderProductCommand = new RelayCommand(_ => OpenAddOrderProductDialog(), _ => CanEditOrderDraft && Products.Count > 0);
        AddOrderItemCommand = new RelayCommand(_ => AddOrderItemToDraft(), _ => CanEditOrderDraft && SelectedOrderItemProduct is not null && OrderItemQuantity > 0);
        RemoveOrderItemCommand = new RelayCommand(_ => RemoveOrderItemFromDraft(), _ => CanEditOrderDraft && SelectedOrderItem is not null);
        BeginAddProductCommand = new RelayCommand(_ => BeginAddProduct(), _ => CanManageProducts);
        BeginEditProductCommand = new RelayCommand(_ => BeginEditProduct(), _ => CanManageProducts && SelectedProduct is not null);
        SaveProductCommand = new RelayCommand(async _ => await SaveProductAsync(), _ => CanManageProducts && IsProductEditorOpen);
        CancelProductEditCommand = new RelayCommand(_ => CloseProductEditor(), _ => IsProductEditorOpen);
        BeginAddUserCommand = new RelayCommand(_ => BeginAddUser(), _ => CanManageUsers);
        BeginEditUserCommand = new RelayCommand(_ => BeginEditUser(), _ => CanManageUsers && SelectedUser is not null);
        SaveUserCommand = new RelayCommand(_ => SaveUser(), _ => CanManageUsers && IsUserEditorOpen);
        CancelUserEditCommand = new RelayCommand(_ => CloseUserEditor(), _ => IsUserEditorOpen);

        _selectedLoginUser = TestUsers[0];
        _selectedUser = TestUsers[0];
        _selectedItem = _allNavigationItems[0];
        _currentView = _selectedItem.View;
        UpdateOrderStatusFilterOptions();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public LocalizationManager T => LocalizationManager.Instance;

    public IReadOnlyList<LanguageOption> LanguageOptions { get; } =
    [
        new("uk", "Українська"),
        new("en", "English")
    ];

    public LanguageOption SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (_selectedLanguage == value)
            {
                return;
            }

            _selectedLanguage = value;
            OnPropertyChanged();
            T.SetLanguage(value.Code);
        }
    }

    public ObservableCollection<NavigationItem> NavigationItems { get; }

    public ObservableCollection<OrderListItem> Orders { get; } = [];

    public ObservableCollection<KanbanColumn> KanbanColumns { get; } =
    [
        new() { Status = "NewOrder" },
        new() { Status = "Registered" },
        new() { Status = "Granted" },
        new() { Status = "Shipped" },
        new() { Status = "Invoiced" },
        new() { Status = "Cancelled" }
    ];

    public ObservableCollection<OrderDetailItem> EditableOrderItems { get; } = [];

    public ObservableCollection<CustomerListItem> Customers { get; } = [];

    public ObservableCollection<ProductListItem> Products { get; } = [];

    public ObservableCollection<ProductListItem> FilteredProducts { get; } = [];

    public ObservableCollection<TestUser> TestUsers { get; }

    public IReadOnlyList<string> RoleOptions { get; } = ["Admin", "Manager", "Viewer", "Director"];

    public ObservableCollection<string> OrderStatusFilterOptions { get; } = [];

    public ICommand NavigateCommand { get; }

    public ICommand SignOutCommand { get; }

    public ICommand RefreshDataCommand { get; }

    public ICommand ClearOrderFiltersCommand { get; }

    public ICommand ClearProductFiltersCommand { get; }

    public ICommand OpenProductPriceSearchCommand { get; }

    public ICommand OpenLowStockProductsCommand { get; }

    public ICommand ChangeOrderStatusCommand { get; }

    public ICommand BeginCreateOrderCommand { get; }

    public ICommand OpenOrderEditorCommand { get; }

    public ICommand OpenSelectOrderCustomerCommand { get; }

    public ICommand OpenAddOrderProductCommand { get; }

    public ICommand AddOrderItemCommand { get; }

    public ICommand RemoveOrderItemCommand { get; }

    public ICommand BeginAddProductCommand { get; }

    public ICommand BeginEditProductCommand { get; }

    public ICommand SaveProductCommand { get; }

    public ICommand CancelProductEditCommand { get; }

    public ICommand BeginAddUserCommand { get; }

    public ICommand BeginEditUserCommand { get; }

    public ICommand SaveUserCommand { get; }

    public ICommand CancelUserEditCommand { get; }

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
        ? T["MockData"]
        : $"API: {_apiSettings.BaseUrl}";

    public string LoadingStatusText => IsLoading
        ? T["LoadingData"]
        : ApiModeText;

    public ReportSummary Summary
    {
        get => _summary;
        private set
        {
            _summary = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasLowStockProducts));
            OnPropertyChanged(nameof(TotalAmountReportText));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool HasLowStockProducts => Summary.HasLowStockProducts;

    public string TotalAmountReportText => T.Format("TotalAmountLabel", Summary.TotalAmountText);

    public string OrderSearchText
    {
        get => _orderSearchText;
        set
        {
            if (_orderSearchText == value)
            {
                return;
            }

            _orderSearchText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasOrderFilters));
            ApplyOrderFilters();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public string OrderStatusFilter
    {
        get => _orderStatusFilter;
        set
        {
            if (_orderStatusFilter == value)
            {
                return;
            }

            _orderStatusFilter = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasOrderFilters));
            ApplyOrderFilters();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool HasOrderFilters =>
        !string.IsNullOrWhiteSpace(OrderSearchText) || OrderStatusFilter != T["All"];

    public string OrderFilterSummaryText => _allOrders.Count == Orders.Count
        ? T.Format("ShownAllOrders", Orders.Count)
        : T.Format("ShownOrders", Orders.Count, _allOrders.Count);

    public OrderListItem? SelectedOrder
    {
        get => _selectedOrder;
        set
        {
            if (_selectedOrder == value)
            {
                return;
            }

            _selectedOrder = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedOrder));
            _ = LoadSelectedOrderDetailsAsync();
        }
    }

    public bool HasSelectedOrder => SelectedOrder is not null;

    public OrderDetail? SelectedOrderDetails
    {
        get => _selectedOrderDetails;
        private set
        {
            if (_selectedOrderDetails == value)
            {
                return;
            }

            _selectedOrderDetails = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedOrderDetails));
            OnPropertyChanged(nameof(IsSelectedOrderEditable));
            OnPropertyChanged(nameof(CanEditSelectedOrder));
            OnPropertyChanged(nameof(CanEditOrderDraft));
            OnOrderStatusActionPropertiesChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool HasSelectedOrderDetails => SelectedOrderDetails is not null;

    public bool IsSelectedOrderEditable => SelectedOrderDetails?.Status is "NewOrder" or "Registered";

    public bool CanEditSelectedOrder => CanManageOrders
        && IsSelectedOrderEditable
        && HasSelectedOrderDetails
        && Products.Count > 0;

    public bool CanEditOrderDraft => CanManageOrders
        && (IsCreatingOrder || IsSelectedOrderEditable)
        && Products.Count > 0;

    public bool IsCreatingOrder
    {
        get => _isCreatingOrder;
        private set
        {
            if (_isCreatingOrder == value)
            {
                return;
            }

            _isCreatingOrder = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsEditingOrder));
            OnPropertyChanged(nameof(CanEditOrderDraft));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool IsEditingOrder => !IsCreatingOrder;

    public string OrderDraftTotalText => $"{EditableOrderItems.Sum(item => item.LineTotal):N2} грн";

    public OrderDetailItem? SelectedOrderItem
    {
        get => _selectedOrderItem;
        set
        {
            if (_selectedOrderItem == value)
            {
                return;
            }

            _selectedOrderItem = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public CustomerListItem? SelectedOrderCustomer
    {
        get => _selectedOrderCustomer;
        set
        {
            if (_selectedOrderCustomer == value)
            {
                return;
            }

            _selectedOrderCustomer = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public ProductListItem? SelectedOrderItemProduct
    {
        get => _selectedOrderItemProduct;
        set
        {
            if (_selectedOrderItemProduct == value)
            {
                return;
            }

            _selectedOrderItemProduct = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public string? OrderItemEditorError
    {
        get => _orderItemEditorError;
        private set
        {
            if (_orderItemEditorError == value)
            {
                return;
            }

            _orderItemEditorError = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasOrderItemEditorError));
        }
    }

    public bool HasOrderItemEditorError => !string.IsNullOrWhiteSpace(OrderItemEditorError);

    public string OrderNumber
    {
        get => _orderNumber;
        set
        {
            if (_orderNumber == value)
            {
                return;
            }

            _orderNumber = value;
            OnPropertyChanged();
        }
    }

    public string OrderComment
    {
        get => _orderComment;
        set
        {
            if (_orderComment == value)
            {
                return;
            }

            _orderComment = value;
            OnPropertyChanged();
        }
    }

    public int OrderItemQuantity
    {
        get => _orderItemQuantity;
        set
        {
            if (_orderItemQuantity == value)
            {
                return;
            }

            _orderItemQuantity = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool CanRegisterOrder => CanShowOrderStatusAction("register");

    public bool CanGrantOrder => CanShowOrderStatusAction("grant");

    public bool CanShipOrder => CanShowOrderStatusAction("ship");

    public bool CanInvoiceOrder => CanShowOrderStatusAction("invoice");

    public bool CanCancelOrder => CanShowOrderStatusAction("cancel");

    public bool HasAvailableOrderStatusActions =>
        CanRegisterOrder || CanGrantOrder || CanShipOrder || CanInvoiceOrder || CanCancelOrder;

    public bool IsOrderDetailsLoading
    {
        get => _isOrderDetailsLoading;
        private set
        {
            if (_isOrderDetailsLoading == value)
            {
                return;
            }

            _isOrderDetailsLoading = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool IsOrderStatusActionRunning
    {
        get => _isOrderStatusActionRunning;
        private set
        {
            if (_isOrderStatusActionRunning == value)
            {
                return;
            }

            _isOrderStatusActionRunning = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public TestUser? SelectedUser
    {
        get => _selectedUser;
        set
        {
            if (_selectedUser == value)
            {
                return;
            }

            if (IsUserEditorOpen)
            {
                CloseUserEditor();
            }

            _selectedUser = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool IsUserEditorOpen
    {
        get => _isUserEditorOpen;
        private set
        {
            if (_isUserEditorOpen == value)
            {
                return;
            }

            _isUserEditorOpen = value;
            OnPropertyChanged();
        }
    }

    public string UserEditorTitle => _isEditingUser ? T["UserEditorEdit"] : T["UserEditorNew"];

    public string? UserEditorError
    {
        get => _userEditorError;
        private set
        {
            if (_userEditorError == value)
            {
                return;
            }

            _userEditorError = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasUserEditorError));
        }
    }

    public bool HasUserEditorError => !string.IsNullOrWhiteSpace(UserEditorError);

    public string UserLogin
    {
        get => _userLogin;
        set
        {
            if (_userLogin == value)
            {
                return;
            }

            _userLogin = value;
            OnPropertyChanged();
        }
    }

    public string UserFullName
    {
        get => _userFullName;
        set
        {
            if (_userFullName == value)
            {
                return;
            }

            _userFullName = value;
            OnPropertyChanged();
        }
    }

    public string UserRoleName
    {
        get => _userRoleName;
        set
        {
            if (_userRoleName == value)
            {
                return;
            }

            _userRoleName = value;
            OnPropertyChanged();
        }
    }

    public string UserPassword
    {
        get => _userPassword;
        set
        {
            if (_userPassword == value)
            {
                return;
            }

            _userPassword = value;
            OnPropertyChanged();
        }
    }

    public bool UserIsActive
    {
        get => _userIsActive;
        set
        {
            if (_userIsActive == value)
            {
                return;
            }

            _userIsActive = value;
            OnPropertyChanged();
        }
    }

    public bool UserTelegramAllowed
    {
        get => _userTelegramAllowed;
        set
        {
            if (_userTelegramAllowed == value)
            {
                return;
            }

            _userTelegramAllowed = value;
            OnPropertyChanged();
        }
    }

    public string UserTelegramUserId
    {
        get => _userTelegramUserId;
        set
        {
            if (_userTelegramUserId == value)
            {
                return;
            }

            _userTelegramUserId = value;
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

            if (IsProductEditorOpen)
            {
                CloseProductEditor();
            }

            _selectedProduct = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public string ProductSearchText
    {
        get => _productSearchText;
        set
        {
            if (_productSearchText == value)
            {
                return;
            }

            _productSearchText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasProductFilters));
            ApplyProductFilters();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool ShowOnlyLowStockProducts
    {
        get => _showOnlyLowStockProducts;
        set
        {
            if (_showOnlyLowStockProducts == value)
            {
                return;
            }

            _showOnlyLowStockProducts = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasProductFilters));
            ApplyProductFilters();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool HasProductFilters =>
        !string.IsNullOrWhiteSpace(ProductSearchText) || ShowOnlyLowStockProducts;

    public string ProductFilterSummaryText => _allProducts.Count == FilteredProducts.Count
        ? T.Format("ShownAllProducts", FilteredProducts.Count)
        : T.Format("ShownProducts", FilteredProducts.Count, _allProducts.Count);

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

    public string ProductEditorTitle => _isEditingProduct ? T["ProductEditorEdit"] : T["ProductEditorNew"];

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
            OnPermissionPropertiesChanged();

            if (IsAuthenticated)
            {
                RefreshNavigationItems();

                if (!NavigationItems.Contains(SelectedItem) && NavigationItems.Count > 0)
                {
                    SelectedItem = NavigationItems[0];
                }
            }
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

    public bool CanManageUsers => CurrentRoleName is "Admin";

    public bool CanViewReports => CurrentRoleName is "Admin" or "Viewer" or "Director";

    public async Task<bool> SignInAsync(string password)
    {
        if (_apiSettings.UseMockData)
        {
            return SignInLocally(password);
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            LoginError = "Введіть пароль.";
            return false;
        }

        if (_apiClient is not IAuthApiClient authApiClient)
        {
            LoginError = "API-клієнт не підтримує авторизацію.";
            return false;
        }

        try
        {
            AuthenticatedUser user = await authApiClient.SignInAsync(SelectedLoginUser.Login, password);

            LoginError = null;
            IsAuthenticated = true;
            CurrentUserName = user.FullName;
            CurrentRoleName = user.Role;

            if (NavigationItems.Count > 0)
            {
                SelectedItem = NavigationItems[0];
            }

            await LoadClientDataAsync();
            return true;
        }
        catch (Exception exception)
        {
            LoginError = $"Не вдалося увійти через API. Деталі: {exception.Message}";
            return false;
        }
    }

    private bool SignInLocally(string password)
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

        if (!SelectedLoginUser.IsActive)
        {
            LoginError = "Обліковий запис користувача неактивний.";
            return false;
        }

        LoginError = null;
        IsAuthenticated = true;
        CurrentUserName = SelectedLoginUser.FullName;
        CurrentRoleName = SelectedLoginUser.RoleName;
        if (NavigationItems.Count > 0)
        {
            SelectedItem = NavigationItems[0];
        }

        _ = LoadClientDataAsync();

        return true;
    }

    private void SignOut()
    {
        if (_apiClient is IAuthApiClient authApiClient)
        {
            authApiClient.SignOut();
        }

        IsAuthenticated = false;
        CurrentUserName = null;
        CurrentRoleName = null;
        LoginError = null;
        ApiErrorMessage = null;
        _allOrders.Clear();
        _allProducts.Clear();
        _loadedOrderDetails.Clear();
        Orders.Clear();
        ClearKanbanColumns();
        SelectedOrder = null;
        SelectedOrderDetails = null;
        Products.Clear();
        FilteredProducts.Clear();
        Customers.Clear();
        SelectedProduct = null;
        EditableOrderItems.Clear();
        SelectedOrderItem = null;
        OrderSearchText = string.Empty;
        OrderStatusFilter = T["All"];
        ProductSearchText = string.Empty;
        ShowOnlyLowStockProducts = false;
        IsCreatingOrder = false;
        CloseProductEditor();
        CloseUserEditor();
        Summary = new ReportSummary();
        NavigationItems.Clear();
        SelectedItem = _allNavigationItems[0];
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
        OnPropertyChanged(nameof(CanEditSelectedOrder));
        OnOrderStatusActionPropertiesChanged();
    }

    private void RefreshLanguageText()
    {
        string? selectedStatusCode = OrderStatusNames.ToCode(OrderStatusFilter);

        UpdateOrderStatusFilterOptions();
        OrderStatusFilter = selectedStatusCode is null ? T["All"] : OrderStatusNames.ToText(selectedStatusCode);

        OnPropertyChanged(nameof(ApiModeText));
        OnPropertyChanged(nameof(LoadingStatusText));
        OnPropertyChanged(nameof(CurrentTitle));
        OnPropertyChanged(nameof(OrderFilterSummaryText));
        OnPropertyChanged(nameof(ProductFilterSummaryText));
        OnPropertyChanged(nameof(ProductEditorTitle));
        OnPropertyChanged(nameof(UserEditorTitle));
        OnPropertyChanged(nameof(SelectedOrderDetails));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(TotalAmountReportText));

        if (IsAuthenticated)
        {
            RefreshNavigationItems();
        }

        ApplyOrderFilters();
        ApplyProductFilters();
    }

    private void UpdateOrderStatusFilterOptions()
    {
        string[] options =
        [
            T["All"],
            OrderStatusNames.ToText("NewOrder"),
            OrderStatusNames.ToText("Registered"),
            OrderStatusNames.ToText("Granted"),
            OrderStatusNames.ToText("Shipped"),
            OrderStatusNames.ToText("Invoiced"),
            OrderStatusNames.ToText("Cancelled")
        ];

        ReplaceCollection(OrderStatusFilterOptions, options);
    }

    private async Task LoadClientDataAsync()
    {
        IsLoading = true;
        ApiErrorMessage = null;

        try
        {
            int? selectedOrderId = SelectedOrder?.Id;
            IReadOnlyList<OrderListItem> orders = await _apiClient.GetOrdersAsync();
            IReadOnlyList<ProductListItem> products;
            string? productLoadError = null;

            try
            {
                products = await _apiClient.GetProductsAsync();
            }
            catch (Exception exception)
            {
                products = _allProducts.Count > 0 ? _allProducts.ToList() : LocalCatalog.CreateProducts();
                productLoadError = $"Замовлення отримано, але не вдалося завантажити товари з API. Деталі: {exception.Message}";
            }

            _allOrders.Clear();
            _allOrders.AddRange(orders);
            _allProducts.Clear();
            _allProducts.AddRange(products);
            _loadedOrderDetails.Clear();

            foreach (OrderListItem order in orders)
            {
                OrderDetail? details = await _apiClient.GetOrderDetailsAsync(order.Id);
                if (details is not null)
                {
                    _loadedOrderDetails.Add(details);
                }
            }

            ApplyOrderFilters();
            RefreshCustomersFromOrders(_allOrders);
            OrderListItem? nextSelectedOrder = Orders.FirstOrDefault(order => order.Id == selectedOrderId)
                ?? Orders.FirstOrDefault();

            if (SelectedOrder == nextSelectedOrder)
            {
                await LoadSelectedOrderDetailsAsync();
            }
            else
            {
                SelectedOrder = nextSelectedOrder;
            }

            ReplaceCollection(Products, _allProducts);
            ApplyProductFilters();
            SelectedProduct = FilteredProducts.FirstOrDefault() ?? Products.FirstOrDefault();
            SelectedOrderItemProduct ??= Products.FirstOrDefault(product => product.IsActive) ?? Products.FirstOrDefault();
            OnPropertyChanged(nameof(CanEditSelectedOrder));
            OnPropertyChanged(nameof(CanEditOrderDraft));
            Summary = BuildReportSummary(_allOrders, _allProducts, _loadedOrderDetails);

            if (productLoadError is not null)
            {
                ApiErrorMessage = productLoadError;
            }
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

    private async Task LoadSelectedOrderDetailsAsync()
    {
        if (SelectedOrder is null)
        {
            SelectedOrderDetails = null;
            return;
        }

        IsOrderDetailsLoading = true;

        try
        {
            OrderDetail? details = await _apiClient.GetOrderDetailsAsync(SelectedOrder.Id);
            SelectedOrderDetails = null;
            SelectedOrderDetails = details;
            SelectedOrderItem = details?.Items.FirstOrDefault();
        }
        catch (Exception exception)
        {
            SelectedOrderDetails = null;
            SelectedOrderItem = null;
            ApiErrorMessage = $"Не вдалося отримати деталі замовлення. Деталі: {exception.Message}";
        }
        finally
        {
            IsOrderDetailsLoading = false;
        }
    }

    private async Task ChangeOrderStatusAsync(string? action)
    {
        if (!CanExecuteOrderStatusAction(action) || SelectedOrderDetails is null)
        {
            return;
        }

        IsOrderStatusActionRunning = true;
        ApiErrorMessage = null;

        try
        {
            await _apiClient.ChangeOrderStatusAsync(SelectedOrderDetails.Id, action!);
            await LoadClientDataAsync();
        }
        catch (Exception exception)
        {
            ApiErrorMessage = $"Не вдалося змінити стан замовлення. Деталі: {exception.Message}";
        }
        finally
        {
            IsOrderStatusActionRunning = false;
        }
    }

    private bool CanExecuteOrderStatusAction(string? action)
    {
        return !IsOrderStatusActionRunning
            && !IsLoading
            && !IsOrderDetailsLoading
            && CanShowOrderStatusAction(action);
    }

    private bool CanShowOrderStatusAction(string? action)
    {
        if (!CanManageOrders || SelectedOrderDetails is null || string.IsNullOrWhiteSpace(action))
        {
            return false;
        }

        return (SelectedOrderDetails.Status, action.ToLowerInvariant()) switch
        {
            ("NewOrder", "register") => true,
            ("NewOrder", "cancel") => true,
            ("Registered", "grant") => true,
            ("Registered", "cancel") => true,
            ("Granted", "ship") => true,
            ("Granted", "cancel") => true,
            ("Shipped", "invoice") => true,
            _ => false
        };
    }

    private void OnOrderStatusActionPropertiesChanged()
    {
        OnPropertyChanged(nameof(CanRegisterOrder));
        OnPropertyChanged(nameof(CanGrantOrder));
        OnPropertyChanged(nameof(CanShipOrder));
        OnPropertyChanged(nameof(CanInvoiceOrder));
        OnPropertyChanged(nameof(CanCancelOrder));
        OnPropertyChanged(nameof(HasAvailableOrderStatusActions));
        CommandManager.InvalidateRequerySuggested();
    }

    private void BeginCreateOrder()
    {
        IsCreatingOrder = true;
        OrderNumber = $"ORD-{DateTime.Now:yyyyMMdd-HHmmss}";
        OrderComment = string.Empty;
        EditableOrderItems.Clear();
        SelectedOrderItem = null;
        SelectedOrderCustomer = Customers.FirstOrDefault();
        OrderItemEditorError = null;

        OpenOrderEditorWindow();
    }

    private void OpenOrderEditor()
    {
        if (!CanEditSelectedOrder || SelectedOrderDetails is null)
        {
            return;
        }

        IsCreatingOrder = false;
        OrderNumber = SelectedOrderDetails.Number;
        SelectedOrderCustomer = Customers.FirstOrDefault(customer => customer.Id == SelectedOrderDetails.CustomerId)
            ?? new CustomerListItem { Id = SelectedOrderDetails.CustomerId, FullName = SelectedOrderDetails.Customer };
        OrderComment = SelectedOrderDetails.Comment ?? string.Empty;
        EditableOrderItems.Clear();

        foreach (OrderDetailItem item in SelectedOrderDetails.Items)
        {
            EditableOrderItems.Add(CloneOrderItem(item));
        }

        SelectedOrderItem = EditableOrderItems.FirstOrDefault();
        OnPropertyChanged(nameof(OrderDraftTotalText));
        OrderItemEditorError = null;

        OpenOrderEditorWindow();
    }

    private void OpenOrderEditorWindow()
    {
        try
        {
            OrderEditorWindow window = new()
            {
                DataContext = this,
                Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(item => item.IsActive),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            window.ShowDialog();
        }
        catch (Exception exception)
        {
            OrderItemEditorError = $"Не вдалося відкрити редактор замовлення. Деталі: {exception.Message}";
            MessageBox.Show(
                exception.Message,
                "Помилка редактора замовлення",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OpenAddOrderProductDialog()
    {
        if (!CanEditOrderDraft)
        {
            return;
        }

        SelectedOrderItemProduct = Products.FirstOrDefault(product => product.IsActive) ?? Products.FirstOrDefault();
        OrderItemQuantity = 1;
        OrderItemEditorError = null;

        AddOrderProductWindow window = new()
        {
            DataContext = this,
            Owner = Application.Current.Windows.OfType<OrderEditorWindow>().FirstOrDefault(item => item.IsActive)
                ?? Application.Current.Windows.OfType<Window>().FirstOrDefault(item => item.IsActive),
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        window.ShowDialog();
    }

    private void OpenSelectOrderCustomerDialog()
    {
        CustomerSelectionWindow window = new()
        {
            DataContext = this,
            Owner = Application.Current.Windows.OfType<OrderEditorWindow>().FirstOrDefault(item => item.IsActive)
                ?? Application.Current.Windows.OfType<Window>().FirstOrDefault(item => item.IsActive),
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        window.ShowDialog();
    }

    private void AddOrderItemToDraft()
    {
        if (!ValidateOrderItemForm())
        {
            return;
        }

        ProductListItem product = SelectedOrderItemProduct!;
        if (!HasAvailableStock(product.Id, OrderItemQuantity, out string? stockError))
        {
            OrderItemEditorError = stockError;
            return;
        }

        OrderDetailItem? existingItem = EditableOrderItems.FirstOrDefault(item => item.ProductId == product.Id);

        if (existingItem is not null)
        {
            existingItem.Quantity += OrderItemQuantity;
            SelectedOrderItem = existingItem;
        }
        else
        {
            OrderDetailItem item = new()
            {
                Id = 0,
                ProductId = product.Id,
                ProductName = product.Name,
                Quantity = OrderItemQuantity,
                UnitPrice = product.Price,
                LineTotal = product.Price * OrderItemQuantity
            };

            EditableOrderItems.Add(item);
            SelectedOrderItem = item;
        }

        OrderItemEditorError = null;
        OnPropertyChanged(nameof(OrderDraftTotalText));
        CommandManager.InvalidateRequerySuggested();
    }

    private void RemoveOrderItemFromDraft()
    {
        if (SelectedOrderItem is null)
        {
            return;
        }

        int index = EditableOrderItems.IndexOf(SelectedOrderItem);
        EditableOrderItems.Remove(SelectedOrderItem);
        SelectedOrderItem = EditableOrderItems.ElementAtOrDefault(Math.Min(index, EditableOrderItems.Count - 1));
        OrderItemEditorError = null;
        OnPropertyChanged(nameof(OrderDraftTotalText));
    }

    public async Task<bool> SaveOrderEditorAsync()
    {
        if (!ValidateOrderDraft())
        {
            return false;
        }

        ApiErrorMessage = null;
        IsOrderDetailsLoading = true;

        try
        {
            if (IsCreatingOrder)
            {
                OrderDetail createdOrder = await _apiClient.CreateOrderAsync(new CreateOrderRequest
                {
                    OrderNumber = OrderNumber.Trim(),
                    CustomerId = SelectedOrderCustomer!.Id,
                    Comment = string.IsNullOrWhiteSpace(OrderComment) ? null : OrderComment.Trim(),
                    Items = EditableOrderItems
                        .Select(item => new CreateOrderItemRequest { ProductId = item.ProductId, Quantity = item.Quantity })
                        .ToList()
                });

                await LoadClientDataAsync();
                SelectedOrder = Orders.FirstOrDefault(order => order.Id == createdOrder.Id) ?? Orders.FirstOrDefault();
            }
            else if (SelectedOrderDetails is not null)
            {
                await SaveEditedOrderItemsAsync(SelectedOrderDetails);
                await LoadClientDataAsync();
                await LoadSelectedOrderDetailsAsync();
            }

            IsCreatingOrder = false;
            OrderItemEditorError = null;
            return true;
        }
        catch (Exception exception)
        {
            OrderItemEditorError = $"Не вдалося зберегти замовлення. Деталі: {exception.Message}";
            return false;
        }
        finally
        {
            IsOrderDetailsLoading = false;
        }
    }

    private async Task SaveEditedOrderItemsAsync(OrderDetail order)
    {
        foreach (OrderDetailItem originalItem in order.Items.Where(original => EditableOrderItems.All(item => item.Id != original.Id)))
        {
            await _apiClient.RemoveOrderItemAsync(order.Id, originalItem.Id);
        }

        foreach (OrderDetailItem item in EditableOrderItems)
        {
            if (item.Id == 0)
            {
                await _apiClient.AddOrderItemAsync(order.Id, new CreateOrderItemRequest
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity
                });
                continue;
            }

            OrderDetailItem? originalItem = order.Items.FirstOrDefault(original => original.Id == item.Id);
            if (originalItem is not null && originalItem.Quantity != item.Quantity)
            {
                await _apiClient.ReplaceOrderItemAsync(order.Id, item.Id, new CreateOrderItemRequest
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity
                });
            }
        }
    }

    private bool ValidateOrderDraft()
    {
        if (IsCreatingOrder)
        {
            if (string.IsNullOrWhiteSpace(OrderNumber))
            {
                OrderItemEditorError = "Вкажіть номер замовлення.";
                return false;
            }

            if (SelectedOrderCustomer is null)
            {
                OrderItemEditorError = "Оберіть клієнта.";
                return false;
            }
        }

        if (EditableOrderItems.Count == 0)
        {
            OrderItemEditorError = "Додайте хоча б один товар.";
            return false;
        }

        if (EditableOrderItems.Any(item => item.Quantity <= 0))
        {
            OrderItemEditorError = "Кількість товару має бути більшою за 0.";
            return false;
        }

        string? stockError = EditableOrderItems
            .GroupBy(item => item.ProductId)
            .Select(group => GetStockError(group.Key, group.Sum(item => item.Quantity)))
            .FirstOrDefault(error => error is not null);

        if (stockError is not null)
        {
            OrderItemEditorError = stockError;
            return false;
        }

        OrderItemEditorError = null;
        return true;
    }

    private static OrderDetailItem CloneOrderItem(OrderDetailItem item)
    {
        return new OrderDetailItem
        {
            Id = item.Id,
            ProductId = item.ProductId,
            ProductName = item.ProductName,
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice,
            LineTotal = item.LineTotal
        };
    }

    private bool HasAvailableStock(int productId, int quantityToAdd, out string? error)
    {
        int requestedQuantity = EditableOrderItems
            .Where(item => item.ProductId == productId)
            .Sum(item => item.Quantity) + quantityToAdd;

        error = GetStockError(productId, requestedQuantity);
        return error is null;
    }

    private string? GetStockError(int productId, int requestedQuantity)
    {
        ProductListItem? product = Products.FirstOrDefault(item => item.Id == productId);
        return product is not null && requestedQuantity > product.Quantity
            ? $"Недостатньо товару '{product.Name}'. Доступно: {product.Quantity}."
            : null;
    }

    private static ReportSummary BuildReportSummary(
        IEnumerable<OrderListItem> orders,
        IEnumerable<ProductListItem> products,
        IEnumerable<OrderDetail> orderDetails)
    {
        List<OrderListItem> orderList = orders.ToList();
        ProductListItem? topProduct = null;
        int topProductQuantity = 0;

        var topProductGroup = orderDetails
            .SelectMany(order => order.Items)
            .GroupBy(item => item.ProductName)
            .Select(group => new { ProductName = group.Key, Quantity = group.Sum(item => item.Quantity) })
            .OrderByDescending(item => item.Quantity)
            .FirstOrDefault();

        if (topProductGroup is not null)
        {
            topProduct = products.FirstOrDefault(product => product.Name == topProductGroup.ProductName);
            topProductQuantity = topProductGroup.Quantity;
        }

        return new ReportSummary
        {
            TotalOrders = orderList.Count,
            NewOrders = orderList.Count(order => order.Status == "NewOrder"),
            OrdersInProgress = orderList.Count(order => order.Status is "Registered" or "Granted"),
            ShippedOrders = orderList.Count(order => order.Status == "Shipped"),
            InvoicedOrders = orderList.Count(order => order.Status == "Invoiced"),
            CancelledOrders = orderList.Count(order => order.Status == "Cancelled"),
            TotalAmount = orderList.Sum(order => order.Total),
            AverageOrderAmount = orderList.Count == 0 ? 0 : orderList.Average(order => order.Total),
            LowStockProducts = products.Count(IsLowStockProduct),
            TopProductName = topProduct?.Name ?? topProductGroup?.ProductName ?? "-",
            TopProductQuantity = topProductQuantity
        };
    }

    private void EditableOrderItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (OrderDetailItem item in e.OldItems)
            {
                item.PropertyChanged -= EditableOrderItem_PropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (OrderDetailItem item in e.NewItems)
            {
                item.PropertyChanged += EditableOrderItem_PropertyChanged;
            }
        }

        OnPropertyChanged(nameof(OrderDraftTotalText));
        CommandManager.InvalidateRequerySuggested();
    }

    private void EditableOrderItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OrderDetailItem.Quantity) || e.PropertyName == nameof(OrderDetailItem.LineTotalText))
        {
            OnPropertyChanged(nameof(OrderDraftTotalText));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void RefreshCustomersFromOrders(IEnumerable<OrderListItem> orders)
    {
        Dictionary<int, CustomerListItem> customers = LocalCatalog.CreateCustomers()
            .ToDictionary(customer => customer.Id);

        foreach (OrderListItem order in orders)
        {
            if (order.CustomerId <= 0 || customers.ContainsKey(order.CustomerId))
            {
                continue;
            }

            customers[order.CustomerId] = new CustomerListItem
            {
                Id = order.CustomerId,
                FullName = order.Customer
            };
        }

        ReplaceCollection(Customers, customers.Values.OrderBy(customer => customer.FullName));
    }

    private bool ValidateOrderItemForm()
    {
        if (SelectedOrderItemProduct is null)
        {
            OrderItemEditorError = "Оберіть товар.";
            return false;
        }

        if (OrderItemQuantity <= 0)
        {
            OrderItemEditorError = "Кількість товару має бути більшою за 0.";
            return false;
        }

        OrderItemEditorError = null;
        return true;
    }

    private void ApplyOrderFilters()
    {
        IEnumerable<OrderListItem> filteredOrders = _allOrders;

        if (!string.IsNullOrWhiteSpace(OrderSearchText))
        {
            string query = OrderSearchText.Trim();
            filteredOrders = filteredOrders.Where(order =>
                order.Number.Contains(query, StringComparison.OrdinalIgnoreCase)
                || order.Customer.Contains(query, StringComparison.OrdinalIgnoreCase)
                || order.Status.Contains(query, StringComparison.OrdinalIgnoreCase)
                || order.StatusText.Contains(query, StringComparison.OrdinalIgnoreCase)
                || order.TotalText.Contains(query, StringComparison.OrdinalIgnoreCase)
                || order.CreatedAtText.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        string? statusCode = OrderStatusNames.ToCode(OrderStatusFilter);
        if (statusCode is not null)
        {
            filteredOrders = filteredOrders.Where(order => order.Status == statusCode);
        }

        List<OrderListItem> visibleOrders = filteredOrders.ToList();
        ReplaceCollection(Orders, visibleOrders);
        RefreshKanbanColumns(visibleOrders);

        if (SelectedOrder is not null && Orders.All(order => order.Id != SelectedOrder.Id))
        {
            SelectedOrder = Orders.FirstOrDefault();
        }

        OnPropertyChanged(nameof(OrderFilterSummaryText));
    }

    private void ClearOrderFilters()
    {
        OrderSearchText = string.Empty;
        OrderStatusFilter = T["All"];
        ApplyOrderFilters();
    }

    private void RefreshKanbanColumns(IEnumerable<OrderListItem> orders)
    {
        foreach (KanbanColumn column in KanbanColumns)
        {
            column.Orders.Clear();

            foreach (OrderListItem order in orders.Where(order => order.Status == column.Status))
            {
                column.Orders.Add(order);
            }

            column.RefreshMetrics();
        }
    }

    private void ClearKanbanColumns()
    {
        foreach (KanbanColumn column in KanbanColumns)
        {
            column.Orders.Clear();
            column.RefreshMetrics();
        }
    }

    private void ApplyProductFilters()
    {
        IEnumerable<ProductListItem> filteredProducts = _allProducts;

        if (!string.IsNullOrWhiteSpace(ProductSearchText))
        {
            string query = ProductSearchText.Trim();
            filteredProducts = filteredProducts.Where(product =>
                product.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || product.Category.Contains(query, StringComparison.OrdinalIgnoreCase)
                || product.PriceText.Contains(query, StringComparison.OrdinalIgnoreCase)
                || product.ActiveText.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        if (ShowOnlyLowStockProducts)
        {
            filteredProducts = filteredProducts.Where(IsLowStockProduct);
        }

        ReplaceCollection(FilteredProducts, filteredProducts.ToList());

        if (SelectedProduct is not null && FilteredProducts.All(product => product.Id != SelectedProduct.Id))
        {
            SelectedProduct = FilteredProducts.FirstOrDefault();
        }

        OnPropertyChanged(nameof(ProductFilterSummaryText));
    }

    private void ClearProductFilters()
    {
        ProductSearchText = string.Empty;
        ShowOnlyLowStockProducts = false;
        ApplyProductFilters();
    }

    private void OpenProductPriceSearch()
    {
        if (SelectedProduct is null)
        {
            return;
        }

        string query = Uri.EscapeDataString($"site:hotline.ua/ua {SelectedProduct.Name} ціна");
        string url = $"https://www.google.com/search?q={query}";

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            ProductEditorError = $"Не вдалося відкрити пошук цін. Деталі: {exception.Message}";
        }
    }

    private void OpenLowStockProducts()
    {
        NavigationItem? productsItem = NavigationItems.FirstOrDefault(item => item.Key == "Products");
        if (productsItem is null)
        {
            return;
        }

        ProductSearchText = string.Empty;
        ShowOnlyLowStockProducts = true;
        SelectedItem = productsItem;
    }

    private static bool IsLowStockProduct(ProductListItem product)
    {
        return product.IsActive && product.Quantity <= 5;
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

    private async Task SaveProductAsync()
    {
        if (!ValidateProductForm())
        {
            return;
        }

        ProductSaveRequest request = new()
        {
            Name = ProductName.Trim(),
            CategoryName = ProductCategory.Trim(),
            Price = ProductPrice,
            Quantity = ProductQuantity,
            IsActive = ProductIsActive
        };

        try
        {
            ProductListItem savedProduct = _isEditingProduct && SelectedProduct is not null
                ? await _apiClient.UpdateProductAsync(SelectedProduct.Id, request)
                : await _apiClient.CreateProductAsync(request);

            ProductListItem? existingProduct = Products.FirstOrDefault(product => product.Id == savedProduct.Id);
            if (existingProduct is not null)
            {
                existingProduct.Name = savedProduct.Name;
                existingProduct.Category = savedProduct.Category;
                existingProduct.Price = savedProduct.Price;
                existingProduct.Quantity = savedProduct.Quantity;
                existingProduct.IsActive = savedProduct.IsActive;
            }
            else
            {
                Products.Add(savedProduct);
            }

            ProductListItem? existingCatalogProduct = _allProducts.FirstOrDefault(product => product.Id == savedProduct.Id);
            if (existingCatalogProduct is not null)
            {
                existingCatalogProduct.Name = savedProduct.Name;
                existingCatalogProduct.Category = savedProduct.Category;
                existingCatalogProduct.Price = savedProduct.Price;
                existingCatalogProduct.Quantity = savedProduct.Quantity;
                existingCatalogProduct.IsActive = savedProduct.IsActive;
            }
            else
            {
                _allProducts.Add(savedProduct);
            }

            SelectedProduct = Products.FirstOrDefault(product => product.Id == savedProduct.Id) ?? savedProduct;
            ApplyProductFilters();
            Summary = BuildReportSummary(_allOrders, _allProducts, _loadedOrderDetails);
            CloseProductEditor();
        }
        catch (Exception exception)
        {
            ProductEditorError = $"Не вдалося зберегти товар. Деталі: {exception.Message}";
        }
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

    private void BeginAddUser()
    {
        _isEditingUser = false;
        UserLogin = string.Empty;
        UserFullName = string.Empty;
        UserRoleName = "Viewer";
        UserPassword = string.Empty;
        UserIsActive = true;
        UserTelegramAllowed = false;
        UserTelegramUserId = string.Empty;
        UserEditorError = null;
        IsUserEditorOpen = true;
        OnPropertyChanged(nameof(UserEditorTitle));
    }

    private void BeginEditUser()
    {
        if (SelectedUser is null)
        {
            UserEditorError = "Оберіть користувача для редагування.";
            return;
        }

        _isEditingUser = true;
        UserLogin = SelectedUser.Login;
        UserFullName = SelectedUser.FullName;
        UserRoleName = SelectedUser.RoleName;
        UserPassword = SelectedUser.Password;
        UserIsActive = SelectedUser.IsActive;
        UserTelegramAllowed = SelectedUser.TelegramAllowed;
        UserTelegramUserId = SelectedUser.TelegramUserId?.ToString() ?? string.Empty;
        UserEditorError = null;
        IsUserEditorOpen = true;
        OnPropertyChanged(nameof(UserEditorTitle));
    }

    private void SaveUser()
    {
        if (!ValidateUserForm(out long? telegramUserId))
        {
            return;
        }

        if (_isEditingUser && SelectedUser is not null)
        {
            SelectedUser.Login = UserLogin.Trim();
            SelectedUser.FullName = UserFullName.Trim();
            SelectedUser.RoleName = UserRoleName;
            SelectedUser.Password = UserPassword;
            SelectedUser.IsActive = UserIsActive;
            SelectedUser.TelegramAllowed = UserTelegramAllowed;
            SelectedUser.TelegramUserId = telegramUserId;

            if (SelectedUser == SelectedLoginUser)
            {
                CurrentUserName = SelectedUser.FullName;
                CurrentRoleName = SelectedUser.RoleName;
            }
        }
        else
        {
            TestUser user = new(UserLogin.Trim(), UserFullName.Trim(), UserRoleName, UserPassword)
            {
                IsActive = UserIsActive,
                TelegramAllowed = UserTelegramAllowed,
                TelegramUserId = telegramUserId
            };

            TestUsers.Add(user);
            SelectedUser = user;
        }

        CloseUserEditor();
    }

    private bool ValidateUserForm(out long? telegramUserId)
    {
        telegramUserId = null;

        if (string.IsNullOrWhiteSpace(UserLogin))
        {
            UserEditorError = "Вкажіть логін користувача.";
            return false;
        }

        if (TestUsers.Any(user => user != SelectedUser && string.Equals(user.Login, UserLogin.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            UserEditorError = "Користувач із таким логіном уже існує.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(UserFullName))
        {
            UserEditorError = "Вкажіть ПІБ користувача.";
            return false;
        }

        if (!RoleOptions.Contains(UserRoleName))
        {
            UserEditorError = "Оберіть коректну роль користувача.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(UserPassword))
        {
            UserEditorError = "Вкажіть пароль користувача.";
            return false;
        }

        if (UserTelegramAllowed && string.IsNullOrWhiteSpace(UserTelegramUserId))
        {
            UserEditorError = "Telegram User ID обов'язковий, якщо Telegram-доступ увімкнений.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(UserTelegramUserId))
        {
            if (!long.TryParse(UserTelegramUserId.Trim(), out long parsedTelegramUserId) || parsedTelegramUserId <= 0)
            {
                UserEditorError = "Telegram User ID має бути додатним числом.";
                return false;
            }

            if (TestUsers.Any(user => user != SelectedUser && user.TelegramUserId == parsedTelegramUserId))
            {
                UserEditorError = "Користувач із таким Telegram User ID уже існує.";
                return false;
            }

            telegramUserId = parsedTelegramUserId;
        }

        UserEditorError = null;
        return true;
    }

    private void CloseUserEditor()
    {
        UserEditorError = null;
        IsUserEditorOpen = false;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
