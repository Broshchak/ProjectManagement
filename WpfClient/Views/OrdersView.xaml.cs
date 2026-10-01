using System.Windows.Controls;
using System.Windows.Input;
using WpfClient.ViewModels;

namespace WpfClient.Views;

public partial class OrdersView : UserControl
{
    public OrdersView()
    {
        InitializeComponent();
    }

    private void OrdersGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && viewModel.OpenOrderEditorCommand.CanExecute(null))
        {
            viewModel.OpenOrderEditorCommand.Execute(null);
        }
    }
}
