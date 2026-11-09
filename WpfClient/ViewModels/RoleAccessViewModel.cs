using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using WpfClient.Models;
using WpfClient.Services;

namespace WpfClient.ViewModels;

public sealed class RoleAccessViewModel : INotifyPropertyChanged
{
    private readonly IRoleAccessApiClient _api;
    private readonly Func<Task> _onChanged;
    private readonly Dictionary<int, RoleVisibility> _visibility = [];
    private IReadOnlyList<PermissionOption> _catalog = [];
    private CancellationTokenSource _lifetime = new();
    private RoleInfo? _selectedRole;
    private string _editName = "";
    private string _newCode = "";
    private string _newName = "";
    private string? _error;
    private bool _busy;

    public RoleAccessViewModel(IRoleAccessApiClient api, Func<Task> onChanged)
    {
        _api = api;
        _onChanged = onChanged;
        RefreshCommand = new RelayCommand(async _ => await LoadAsync(), _ => IsIdle);
        CreateCommand = new RelayCommand(async _ => await RunAsync(async ct =>
        {
            var role = await _api.CreateRoleAsync(NewCode, NewName, ct);
            ct.ThrowIfCancellationRequested();
            NewCode = "";
            NewName = "";
            ShowRole(role);
            await _onChanged();
        }), _ => IsIdle);
        RenameCommand = new RelayCommand(async _ => await ChangeRoleAsync((role, ct) => _api.RenameRoleAsync(role.Id, EditName, role.Version, ct)),
            _ => IsIdle && SelectedRole is not null);
        ToggleActiveCommand = new RelayCommand(async _ =>
        {
            if (SelectedRole is not { } role ||
                role.IsActive && !Confirm(T["RoleConfirmBlock"])) return;
            await ChangeRoleAsync((current, ct) => _api.SetRoleActiveAsync(current.Id, !current.IsActive, current.Version, ct));
        }, _ => IsIdle && SelectedRole is { Code: not "Admin" });
        DeleteCommand = new RelayCommand(async _ =>
        {
            if (SelectedRole is not { IsSystem: false } role || !Confirm(T["RoleConfirmDelete"])) return;
            await RunAsync(async ct =>
            {
                await _api.DeleteRoleAsync(role.Id, role.Version, ct);
                ct.ThrowIfCancellationRequested();
                _visibility.Remove(role.Id);
                Roles.Remove(role);
                SelectedRole = Roles.FirstOrDefault();
                await _onChanged();
            });
        }, _ => IsIdle && SelectedRole is { IsSystem: false });
        SaveVisibilityCommand = new RelayCommand(async _ =>
        {
            if (SelectedRole is not { Code: not "TelegramBot" } role) return;
            var codes = Choices.Where(p => p.Enabled).Select(p => p.Code).ToArray();
            if (Choices.Any(p => p.Enabled && p.Requires is { } required && !codes.Contains(required)))
            {
                Error = T["RoleBoardRequiresOrders"];
                return;
            }
            await RunAsync(async ct =>
            {
                var saved = await _api.SaveRoleVisibilityAsync(role.Id, role.Version, codes, ct);
                ct.ThrowIfCancellationRequested();
                _visibility[role.Id] = saved;
                ShowRole(role with { Version = saved.Revision });
                await _onChanged();
            });
        }, _ => IsIdle && CanEditVisibility);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public LocalizationManager T => LocalizationManager.Instance;
    public ObservableCollection<RoleInfo> Roles { get; } = [];
    public ObservableCollection<PermissionChoice> Choices { get; } = [];
    public ICommand RefreshCommand { get; }
    public ICommand CreateCommand { get; }
    public ICommand RenameCommand { get; }
    public ICommand ToggleActiveCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand SaveVisibilityCommand { get; }
    public bool IsIdle => !_busy;
    public bool CanEditVisibility => SelectedRole is { Code: not "TelegramBot" };
    public string RoleStatus => SelectedRole is null ? "" :
        $"{T[SelectedRole.IsActive ? "RoleActive" : "RoleBlocked"]} · {T[SelectedRole.IsSystem ? "RoleSystem" : "RoleCustom"]}";
    public string ToggleLabel => T[SelectedRole?.IsActive == false ? "RoleUnblock" : "RoleBlock"];
    public string NewCode { get => _newCode; set { _newCode = value; Notify(); } }
    public string NewName { get => _newName; set { _newName = value; Notify(); } }
    public string EditName { get => _editName; set { _editName = value; Notify(); } }
    public string? Error { get => _error; private set { _error = value; Notify(); } }

    public RoleInfo? SelectedRole
    {
        get => _selectedRole;
        set
        {
            if (_selectedRole == value) return;
            _selectedRole = value;
            EditName = value?.Name ?? "";
            Choices.Clear();
            if (value is not null && CanEditVisibility)
            {
                _visibility.TryGetValue(value.Id, out var visibility);
                foreach (var option in _catalog)
                    Choices.Add(new(option.Code, option.Requires, visibility?.Permissions.Contains(option.Code) == true));
            }
            Notify();
            Notify(nameof(CanEditVisibility));
            Notify(nameof(RoleStatus));
            Notify(nameof(ToggleLabel));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public Task LoadAsync() => RunAsync(async ct =>
    {
        int? selectedId = SelectedRole?.Id;
        var roles = await _api.GetRolesAsync(ct);
        var catalog = await _api.GetPermissionCatalogAsync(ct);
        var visibility = await _api.GetRoleVisibilityAsync(ct);
        ct.ThrowIfCancellationRequested();
        _catalog = catalog;
        _visibility.Clear();
        foreach (var entry in visibility) _visibility[entry.RoleId] = entry;
        // The permissions response may be newer than the roles response.
        // Use the lower revision to reject conflicting edits rather than overwriting them.
        Roles.Clear();
        foreach (var role in roles)
            Roles.Add(_visibility.TryGetValue(role.Id, out var entry) ? role with { Version = Math.Min(role.Version, entry.Revision) } : role);
        SelectedRole = null;
        SelectedRole = Roles.FirstOrDefault(r => r.Id == selectedId) ?? Roles.FirstOrDefault();
    });

    public void Reset()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
        _lifetime = new();
        _busy = false;
        _catalog = [];
        _visibility.Clear();
        Roles.Clear();
        SelectedRole = null;
        Choices.Clear();
        NewName = NewCode = "";
        Error = null;
        Notify(nameof(IsIdle));
    }

    private Task ChangeRoleAsync(Func<RoleInfo, CancellationToken, Task<RoleInfo>> change) => RunAsync(async ct =>
    {
        if (SelectedRole is not { } role) return;
        var updated = await change(role, ct);
        ct.ThrowIfCancellationRequested();
        if (_visibility.TryGetValue(role.Id, out var visibility))
            _visibility[role.Id] = visibility with { Revision = updated.Version };
        ShowRole(updated);
        await _onChanged();
    });

    private async Task RunAsync(Func<CancellationToken, Task> action)
    {
        if (_busy) return;
        var ct = _lifetime.Token;
        _busy = true;
        Notify(nameof(IsIdle));
        CommandManager.InvalidateRequerySuggested();
        Error = null;
        try { await action(ct); }
        catch (Exception ex) { if (!ct.IsCancellationRequested) Error = ex.Message; }
        finally
        {
            if (!ct.IsCancellationRequested)
            {
                _busy = false;
                Notify(nameof(IsIdle));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    private void ShowRole(RoleInfo updated)
    {
        var current = Roles.FirstOrDefault(role => role.Id == updated.Id);
        if (current is null) Roles.Add(updated);
        else Roles[Roles.IndexOf(current)] = updated;
        SelectedRole = null;
        SelectedRole = updated;
    }

    private bool Confirm(string message) =>
        MessageBox.Show(message, T["NavRoles"], MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

    private void Notify([CallerMemberName] string? property = null) => PropertyChanged?.Invoke(this, new(property));
}

public sealed class PermissionChoice(string code, string? requires, bool enabled)
{
    public string Code { get; } = code;
    public string? Requires { get; } = requires;
    public string Label => LocalizationManager.Instance[Code];
    public bool Enabled { get; set; } = enabled;
}
