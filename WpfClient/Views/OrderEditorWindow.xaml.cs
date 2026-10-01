using System.Windows;
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
        if (DataContext is MainViewModel viewModel && await viewModel.SaveOrderEditorAsync())
        {
            Close();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
