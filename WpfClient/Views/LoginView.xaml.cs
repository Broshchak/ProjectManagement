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

    private async void SignIn_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        await TrySignInAsync();
    }

    private async void PasswordInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await TrySignInAsync();
        }
    }

    private void UserSelection_Changed(object sender, SelectionChangedEventArgs e)
    {
        PasswordInput.Clear();
        PasswordInput.Focus();
    }

    private async Task TrySignInAsync()
    {
        if (DataContext is MainViewModel viewModel && await viewModel.SignInAsync(PasswordInput.Password))
        {
            PasswordInput.Clear();
        }
    }
}
