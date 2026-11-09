using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Orders.Workflow;
using WpfClient.Models;
using WpfClient.Services;

namespace WpfClient.ViewModels;

public sealed class WorkflowEditorViewModel : WorkflowEditorRow
{
    private static readonly string[] CategoryCodes = ["New", "InProgress", "Shipped", "Completed", "Cancelled"];
    private readonly IWorkflowApiClient _api;
    private readonly Func<Task> _onPublished;
    private WorkflowDraft? _draft;
    private IReadOnlyList<WorkflowOption> _roles = [];
    private IReadOnlyList<WorkflowOption> _rules = [];
    private bool _busy;
    private bool _dirty;
    private bool _validated;
    private int _loadVersion;
    private string? _message;
    private string? _error;
    private WorkflowStateRow? _selectedState;
    private WorkflowTransitionRow? _selectedTransition;

    public WorkflowEditorViewModel(IWorkflowApiClient api, Func<Task> onPublished)
    {
        _api = api;
        _onPublished = onPublished;
        RefreshCommand = new RelayCommand(async _ =>
        {
            if (ConfirmDiscard()) await LoadAsync(reload: true);
        }, _ => !_busy);
        SaveCommand = new RelayCommand(async _ => await RunAsync(async version =>
        {
            var definition = new WorkflowDefinition(States.Select(s => s.Build()).ToArray(),
                Transitions.Select(t => t.Build()).ToArray());
            var saved = await _api.SaveWorkflowDraftAsync(new(_draft!.Version, definition));
            if (version != _loadVersion) return;
            _draft = saved;
            _validated = false;
            IsDirty = false;
            Message = T["WorkflowDraftSaved"];
        }), _ => IsReady && IsDirty);
        ValidateCommand = new RelayCommand(async _ => await RunAsync(async version =>
        {
            var validation = await _api.ValidateWorkflowDraftAsync(CurrentVersion());
            if (version != _loadVersion) return;
            _validated = validation.IsValid;
            foreach (var error in validation.Errors) Issues.Add(error);
            Message = T.Format("WorkflowValidationSummary", validation.Errors.Count, validation.AffectedOrders);
        }), _ => IsReady && !IsDirty);
        PublishCommand = new RelayCommand(async _ => await RunAsync(async version =>
        {
            var published = await _api.PublishWorkflowAsync(CurrentVersion());
            if (version != _loadVersion) return;
            _draft = published;
            _validated = false;
            Message = T.Format("WorkflowPublished", published.PublishedRevision);
            await _onPublished();
        }), _ => IsReady && !IsDirty && _validated);
        AddStateCommand = new RelayCommand(_ =>
        {
            short position = (short)Enumerable.Range(1, short.MaxValue).First(i => States.All(s => s.Position != i));
            SelectedState = AddState(new("", "", position, "InProgress", false, false, false));
            Edited();
        }, _ => IsReady && States.Count < 100);
        RemoveStateCommand = new RelayCommand(_ =>
        {
            var state = SelectedState!;
            state.PropertyChanged -= OnEdited;
            States.Remove(state);
            Edited();
        }, _ => IsReady && SelectedState is not null);
        AddTransitionCommand = new RelayCommand(_ =>
        {
            SelectedTransition = AddTransition(new("", "", "", "", [], []));
            Edited();
        }, _ => IsReady && Transitions.Count < 300);
        RemoveTransitionCommand = new RelayCommand(_ =>
        {
            var transition = SelectedTransition!;
            transition.PropertyChanged -= OnEdited;
            Transitions.Remove(transition);
            Edited();
        }, _ => IsReady && SelectedTransition is not null);
    }

    public LocalizationManager T => LocalizationManager.Instance;
    public ObservableCollection<WorkflowStateRow> States { get; } = [];
    public ObservableCollection<WorkflowTransitionRow> Transitions { get; } = [];
    public ObservableCollection<string> Issues { get; } = [];
    public IReadOnlyList<WorkflowOption> Categories => CategoryCodes.Select(code =>
        new WorkflowOption(code, T["WorkflowCategory" + code])).ToArray();

    public void RefreshLanguage() => Notify(nameof(Categories));
    public WorkflowStateRow? SelectedState { get => _selectedState; set => Set(ref _selectedState, value); }
    public WorkflowTransitionRow? SelectedTransition { get => _selectedTransition; set => Set(ref _selectedTransition, value); }
    public bool IsReady => !_busy && _draft is not null;
    public bool IsDirty { get => _dirty; private set => Set(ref _dirty, value); }
    public string? Message { get => _message; private set => Set(ref _message, value); }
    public string? Error { get => _error; private set => Set(ref _error, value); }
    public ICommand RefreshCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand ValidateCommand { get; }
    public ICommand PublishCommand { get; }
    public ICommand AddStateCommand { get; }
    public ICommand RemoveStateCommand { get; }
    public ICommand AddTransitionCommand { get; }
    public ICommand RemoveTransitionCommand { get; }

    public bool ConfirmDiscard() => !IsDirty ||
        MessageBox.Show(T["WorkflowDiscard"], T["NavWorkflow"], MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public Task LoadAsync(bool reload = false) => !reload && _draft is not null ? Task.CompletedTask : RunAsync(async version =>
    {
        var draft = await _api.GetWorkflowDraftAsync();
        var roles = await _api.GetWorkflowRolesAsync();
        var rules = await _api.GetWorkflowRulesAsync();
        if (version != _loadVersion) return;
        ClearRows();
        _draft = draft;
        _validated = false;
        _roles = roles;
        _rules = rules.Select(rule => rule with { Name = T["WorkflowRule" + rule.Code] }).ToArray();
        foreach (var state in draft.Definition.States.OrderBy(s => s.SortOrder)) AddState(state);
        foreach (var transition in draft.Definition.Transitions) AddTransition(transition);
        SelectedState = States.FirstOrDefault();
        SelectedTransition = Transitions.FirstOrDefault();
        IsDirty = false;
        Message = T.Format("WorkflowRevision", draft.PublishedRevision);
    });

    public void Reset()
    {
        _loadVersion++;
        _busy = false;
        _draft = null;
        _validated = false;
        ClearRows();
        IsDirty = false;
        Message = Error = null;
        Issues.Clear();
        Notify(nameof(IsReady));
    }

    private async Task RunAsync(Func<int, Task> action)
    {
        if (_busy) return;
        int version = _loadVersion;
        _busy = true;
        Error = Message = null;
        Issues.Clear();
        Notify(nameof(IsReady));
        CommandManager.InvalidateRequerySuggested();
        try { await action(version); }
        catch (Exception ex)
        {
            if (version == _loadVersion)
                Error = ex is ApiException { StatusCode: HttpStatusCode.Conflict } ? T["WorkflowConflict"] : ex.Message;
        }
        finally
        {
            if (version == _loadVersion)
            {
                _busy = false;
                Notify(nameof(IsReady));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    private PublishWorkflow CurrentVersion() => new(_draft!.Version, _draft.PublishedRevision);

    private WorkflowStateRow AddState(WorkflowState state)
    {
        var row = new WorkflowStateRow(state);
        row.PropertyChanged += OnEdited;
        States.Add(row);
        return row;
    }

    private WorkflowTransitionRow AddTransition(WorkflowTransition transition)
    {
        var row = new WorkflowTransitionRow(transition, _roles, _rules);
        row.PropertyChanged += OnEdited;
        Transitions.Add(row);
        return row;
    }

    private void ClearRows()
    {
        foreach (var state in States) state.PropertyChanged -= OnEdited;
        foreach (var transition in Transitions) transition.PropertyChanged -= OnEdited;
        States.Clear();
        Transitions.Clear();
    }

    private void OnEdited(object? sender, PropertyChangedEventArgs args) => Edited();

    private void Edited()
    {
        _validated = false;
        IsDirty = true;
        Message = Error = null;
        Issues.Clear();
        CommandManager.InvalidateRequerySuggested();
    }
}

public abstract class WorkflowEditorRow : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    protected void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        Notify(name);
    }
}

public sealed class WorkflowStateRow(WorkflowState state) : WorkflowEditorRow
{
    private string _code = state.Code;
    private string _name = state.Name;
    private string _sortOrder = state.SortOrder.ToString(CultureInfo.InvariantCulture);
    private string _category = state.Category;
    private string? _color = state.Color;
    private bool _initial = state.IsInitial;
    private bool _final = state.IsFinal;
    private bool _editable = state.CanEditItems;
    private bool _active = state.IsActive;
    public string Code { get => _code; set => Set(ref _code, value); }
    public string Name { get => _name; set => Set(ref _name, value); }
    public string SortOrder { get => _sortOrder; set => Set(ref _sortOrder, value); }
    public string Category { get => _category; set => Set(ref _category, value); }
    public string? Color { get => _color; set => Set(ref _color, value); }
    public bool IsInitial { get => _initial; set => Set(ref _initial, value); }
    public bool IsFinal { get => _final; set => Set(ref _final, value); }
    public bool CanEditItems { get => _editable; set => Set(ref _editable, value); }
    public bool IsActive { get => _active; set => Set(ref _active, value); }
    public short Position => short.TryParse(SortOrder, CultureInfo.InvariantCulture, out short position) ? position : (short)0;
    public WorkflowState Build() => new(Code.Trim(), Name.Trim(), Position, Category, IsInitial, IsFinal, CanEditItems, Color?.Trim(), IsActive);
}

public sealed class WorkflowTransitionRow : WorkflowEditorRow
{
    private string _code;
    private string _name;
    private string? _from;
    private string? _to;
    private bool _active;

    public WorkflowTransitionRow(WorkflowTransition transition, IReadOnlyList<WorkflowOption> roles, IReadOnlyList<WorkflowOption> rules)
    {
        _code = transition.Code;
        _name = transition.Name;
        _from = transition.From;
        _to = transition.To;
        _active = transition.IsActive;
        Roles = Choices(roles, transition.Roles);
        Rules = Choices(rules, transition.Rules);
    }

    public string Code { get => _code; set => Set(ref _code, value); }
    public string Name { get => _name; set => Set(ref _name, value); }
    public string? From { get => _from; set => Set(ref _from, value); }
    public string? To { get => _to; set => Set(ref _to, value); }
    public bool IsActive { get => _active; set => Set(ref _active, value); }
    public IReadOnlyList<WorkflowChoice> Roles { get; }
    public IReadOnlyList<WorkflowChoice> Rules { get; }
    public WorkflowTransition Build() => new(Code.Trim(), Name.Trim(), From ?? "", To ?? "",
        Roles.Where(c => c.Enabled).Select(c => c.Code).ToArray(),
        Rules.Where(c => c.Enabled).Select(c => c.Code).ToArray(), IsActive);

    private WorkflowChoice[] Choices(IReadOnlyList<WorkflowOption> options, IReadOnlyList<string>? checkedCodes)
    {
        var selected = checkedCodes ?? [];
        return options.Concat(selected.Where(code => options.All(option => option.Code != code))
                .Select(code => new WorkflowOption(code, code)))
            .Select(option =>
            {
                var choice = new WorkflowChoice(option, selected.Contains(option.Code));
                choice.PropertyChanged += (_, _) => Notify();
                return choice;
            }).ToArray();
    }
}

public sealed class WorkflowChoice(WorkflowOption option, bool enabled) : WorkflowEditorRow
{
    private bool _enabled = enabled;
    public string Code => option.Code;
    public string Name => option.Name;
    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }
}
