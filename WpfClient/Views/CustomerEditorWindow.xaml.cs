using System.Windows;
using WpfClient.Models;

namespace WpfClient.Views;

public partial class CustomerEditorWindow : Window
{
    private readonly Func<SaveCustomerRequest, Task<CustomerListItem>> _save;
    public CustomerEditorWindow(Func<SaveCustomerRequest, Task<CustomerListItem>> save, CustomerListItem? customer = null)
    {
        InitializeComponent();
        _save = save;
        DataContext = new SaveCustomerRequest
        {
            FullName = customer?.FullName ?? "", Phone = customer?.Phone, Email = customer?.Email, Address = customer?.Address
        };
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!Form.IsEnabled || DataContext is not SaveCustomerRequest request) return;
        if (string.IsNullOrWhiteSpace(request.FullName)) { ErrorText.Text = LocalizationManager.Instance["CustomerNameRequired"]; return; }
        Form.IsEnabled = false;
        try { await _save(request); DialogResult = true; }
        catch (Exception ex) { ErrorText.Text = ex.Message; }
        finally { Form.IsEnabled = true; }
    }
}
