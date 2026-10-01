using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using WpfClient.Models;
using WpfClient.ViewModels;

namespace WpfClient.Views;

public partial class AddOrderProductWindow : Window
{
    public AddOrderProductWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => ConfigureFilter();
    }

    private void ConfigureFilter()
    {
        ICollectionView view = CollectionViewSource.GetDefaultView(ProductsGrid.ItemsSource);
        if (view is null)
        {
            return;
        }

        view.Filter = item =>
        {
            if (item is not ProductListItem product)
            {
                return false;
            }

            string query = SearchBox.Text.Trim();
            return string.IsNullOrWhiteSpace(query)
                || product.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || product.Category.Contains(query, StringComparison.OrdinalIgnoreCase);
        };
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        CollectionViewSource.GetDefaultView(ProductsGrid.ItemsSource)?.Refresh();
    }

    private void ProductsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        AddSelectedProductAndClose();
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        AddSelectedProductAndClose();
    }

    private void AddSelectedProductAndClose()
    {
        if (DataContext is MainViewModel viewModel && viewModel.AddOrderItemCommand.CanExecute(null))
        {
            viewModel.AddOrderItemCommand.Execute(null);
            if (!viewModel.HasOrderItemEditorError)
            {
                Close();
            }
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
