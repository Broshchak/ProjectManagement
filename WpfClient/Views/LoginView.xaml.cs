using System.Windows.Controls;
using System.Windows.Input;
using WpfClient.ViewModels;

namespace WpfClient.Views;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
    }

    private void SignIn_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        TrySignIn();
    }

    private void PasswordInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            TrySignIn();
        }
    }

    private void UserSelection_Changed(object sender, SelectionChangedEventArgs e)
    {
        PasswordInput.Clear();
        PasswordInput.Focus();
    }

    private void TrySignIn()
    {
        if (DataContext is MainViewModel viewModel && viewModel.SignIn(PasswordInput.Password))
        {
            PasswordInput.Clear();
        }
    }
}
