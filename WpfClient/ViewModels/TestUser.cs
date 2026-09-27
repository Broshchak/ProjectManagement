using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfClient.ViewModels;

public sealed class TestUser : INotifyPropertyChanged
{
    private string _login;
    private string _fullName;
    private string _roleName;
    private string _password;
    private bool _isActive = true;
    private bool _telegramAllowed;
    private long? _telegramUserId;

    public TestUser(string login, string fullName, string roleName, string password)
    {
        _login = login;
        _fullName = fullName;
        _roleName = roleName;
        _password = password;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Login
    {
        get => _login;
        set
        {
            if (_login == value)
            {
                return;
            }

            _login = value;
            OnPropertyChanged();
        }
    }

    public string FullName
    {
        get => _fullName;
        set
        {
            if (_fullName == value)
            {
                return;
            }

            _fullName = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    public string RoleName
    {
        get => _roleName;
        set
        {
            if (_roleName == value)
            {
                return;
            }

            _roleName = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Role));
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    public string Role
    {
        get => RoleName;
        set => RoleName = value;
    }

    public string Password
    {
        get => _password;
        set
        {
            if (_password == value)
            {
                return;
            }

            _password = value;
            OnPropertyChanged();
        }
    }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value)
            {
                return;
            }

            _isActive = value;
            OnPropertyChanged();
        }
    }

    public bool TelegramAllowed
    {
        get => _telegramAllowed;
        set
        {
            if (_telegramAllowed == value)
            {
                return;
            }

            _telegramAllowed = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TelegramActive));
            OnPropertyChanged(nameof(TelegramAllowedText));
        }
    }

    public long? TelegramUserId
    {
        get => _telegramUserId;
        set
        {
            if (_telegramUserId == value)
            {
                return;
            }

            _telegramUserId = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TelegramActive));
            OnPropertyChanged(nameof(TelegramUserIdText));
        }
    }

    public string DisplayName => $"{FullName} ({RoleName})";

    public bool TelegramActive => TelegramAllowed && TelegramUserId.HasValue;

    public string TelegramAllowedText => TelegramAllowed ? "Дозволено" : "Заборонено";

    public string TelegramUserIdText => TelegramUserId?.ToString() ?? "-";

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
