using System.Windows.Controls;
using System.ComponentModel;
using WpfClient.ViewModels;

namespace WpfClient.Views;

public partial class UsersView : UserControl
{
    public UsersView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is MainViewModel old) old.PropertyChanged -= ViewModelChanged;
            if (e.NewValue is MainViewModel current) current.PropertyChanged += ViewModelChanged;
            SyncPassword();
        };
    }

    private void ViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.UserPassword)) SyncPassword();
    }

    private void SyncPassword()
    {
        if (DataContext is MainViewModel vm && UserPasswordInput.Password != vm.UserPassword)
            UserPasswordInput.Password = vm.UserPassword;
    }

    private void UserPasswordInput_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.UserPassword = UserPasswordInput.Password;
    }
}
