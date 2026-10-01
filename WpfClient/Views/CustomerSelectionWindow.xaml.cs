using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using WpfClient.Models;

namespace WpfClient.Views;

public partial class CustomerSelectionWindow : Window
{
    public CustomerSelectionWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => ConfigureFilter();
    }

    private void ConfigureFilter()
    {
        ICollectionView view = CollectionViewSource.GetDefaultView(CustomersGrid.ItemsSource);
        if (view is null)
        {
            return;
        }

        view.Filter = item =>
        {
            if (item is not CustomerListItem customer)
            {
                return false;
            }

            string query = SearchBox.Text.Trim();
            return string.IsNullOrWhiteSpace(query)
                || customer.FullName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || (customer.Phone?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
                || (customer.Email?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);
        };
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        CollectionViewSource.GetDefaultView(CustomersGrid.ItemsSource)?.Refresh();
    }

    private void CustomersGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        Close();
    }

    private void SelectButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
