using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfClient.ViewModels;

namespace WpfClient.Views;

public partial class OrderEditorWindow : Window
{
    public OrderEditorWindow()
    {
        InitializeComponent();
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (HasValidationError(this)) return;
        if (DataContext is MainViewModel viewModel && await viewModel.SaveOrderEditorAsync())
        {
            Close();
        }
    }

    private static bool HasValidationError(DependencyObject parent)
    {
        if (Validation.GetHasError(parent)) return true;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            if (HasValidationError(VisualTreeHelper.GetChild(parent, i))) return true;
        return false;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
