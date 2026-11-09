using System.ComponentModel;
using System.Windows;
using WpfClient.ViewModels;

namespace WpfClient;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (DataContext is MainViewModel vm && !vm.ConfirmDiscardWorkflow()) e.Cancel = true;
        base.OnClosing(e);
    }

    private void LoginView_Loaded(object sender, RoutedEventArgs e)
    {

    }
}
