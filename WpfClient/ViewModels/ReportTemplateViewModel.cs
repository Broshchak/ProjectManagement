using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Orders.Reports;
using WpfClient.Models;
using WpfClient.Services;

namespace WpfClient.ViewModels;

public sealed class ReportTemplateViewModel : INotifyPropertyChanged
{
    private readonly IReportPdfApiClient api;
    private readonly Func<bool> isCurrent;
    private readonly Func<DownloadedReportPdf, ReportPdfAction, Task<bool>> deliver;
    private readonly Func<bool> confirmDelete;
    private readonly DateTime initialFrom;
    private readonly DateTime initialTo;
    private IReadOnlyList<ReportSource> catalog = [];
    private IReadOnlyList<ReportChoice> publishedStatuses = [];
    private ReportTemplate? selectedTemplate;
    private ReportChoice? selectedSource;
    private ReportChoice? selectedColumn;
    private ReportChoice? availableColumn;
    private string name = "";
    private bool busy;
    private bool loaded;
    private string? message;
    private string? error;
    private bool showTable = true;

    public ReportTemplateViewModel(IReportPdfApiClient api, bool canEdit, DateTime from, DateTime to,
        Func<bool> isCurrent, Func<DownloadedReportPdf, ReportPdfAction, Task<bool>> deliver, Func<bool> confirmDelete)
    {
        this.api = api;
        this.isCurrent = isCurrent;
        this.deliver = deliver;
        this.confirmDelete = confirmDelete;
        initialFrom = from.Date;
        initialTo = to.Date;
        CanEdit = canEdit;
        ReloadCommand = new RelayCommand(async _ => await LoadAsync(), _ => !busy);
        NewCommand = new RelayCommand(_ => ResetDraft(), _ => IsReady);
        SaveCommand = new RelayCommand(async _ => await RunAsync(SaveAsync), _ => CanEditLayout && selectedSource is not null);
        DeleteCommand = new RelayCommand(async _ =>
        {
            if (selectedTemplate is { } template && confirmDelete())
                await RunAsync(async () =>
                {
                    await api.DeleteReportTemplateAsync(template.Id, template.Version);
                    if (!isCurrent()) return;
                    Templates.Remove(template);
                    ResetDraft();
                    Message = T["ReportTemplateDeleted"];
                });
        }, _ => CanEditLayout && selectedTemplate is not null);
        PreviewCommand = new RelayCommand(async _ => await RunAsync(() => GenerateAsync(ReportPdfAction.Preview)), _ => CanGenerate);
        DownloadCommand = new RelayCommand(async _ => await RunAsync(() => GenerateAsync(ReportPdfAction.Save)), _ => CanGenerate);
        PrintCommand = new RelayCommand(async _ => await RunAsync(() => GenerateAsync(ReportPdfAction.Print)), _ => CanGenerate);
        AddColumnCommand = new RelayCommand(_ =>
        {
            Columns.Add(AvailableColumn!);
            SelectedColumn = AvailableColumn;
        }, _ => CanEditLayout && AvailableColumn is { } field && !Columns.Contains(field));
        RemoveColumnCommand = new RelayCommand(_ => Columns.Remove(SelectedColumn!), _ => CanEditLayout && SelectedColumn is not null);
        MoveColumnCommand = new RelayCommand(direction =>
        {
            int index = Columns.IndexOf(SelectedColumn!);
            int target = index + Offset(direction);
            Columns.Move(index, target);
            SelectedColumn = Columns[target];
            CommandManager.InvalidateRequerySuggested();
        }, direction =>
        {
            int index = Columns.IndexOf(SelectedColumn!);
            int target = index + Offset(direction);
            return CanEditLayout && index >= 0 && target >= 0 && target < Columns.Count;
        });
        Columns.CollectionChanged += (_, _) => CommandManager.InvalidateRequerySuggested();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public LocalizationManager T => LocalizationManager.Instance;
    public bool CanEdit { get; }
    public bool IsReady => loaded && !busy;
    public bool CanEditLayout => IsReady && CanEdit && selectedSource is not null;
    public bool CanChooseSource => IsReady && (CanEdit || selectedTemplate is null);
    private bool CanGenerate => IsReady && selectedSource is not null;
    public bool UsesPeriod => selectedSource?.Code != "low-stock";
    public bool IsOrderSource => selectedSource?.Code is "orders" or "summary";
    public bool ShowTable { get => showTable; set => Set(ref showTable, value); }
    public bool ShowSummary { get; set; } = true;
    public bool Landscape { get; set; }
    public bool Descending { get; set; }
    public string? Note { get; set; }
    public string Name { get => name; set => Set(ref name, value); }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? Status { get; set; }
    public string? Category { get; set; }
    public string? GroupBy { get; set; }
    public string? SortBy { get; set; }
    public string? Message { get => message; private set => Set(ref message, value); }
    public string? Error { get => error; private set => Set(ref error, value); }
    public IReadOnlyList<ReportChoice> Sources { get; private set; } = [];
    public IReadOnlyList<ReportChoice> Fields { get; private set; } = [];
    public IReadOnlyList<ReportChoice> SortOptions { get; private set; } = [];
    public IReadOnlyList<ReportChoice> GroupOptions { get; private set; } = [];
    public IReadOnlyList<ReportChoice> StatusOptions { get; private set; } = [];
    public IReadOnlyList<ReportChoice> CategoryOptions => [Any(), .. new[] { "New", "InProgress", "Shipped", "Completed", "Cancelled" }
        .Select(code => new ReportChoice(code, T["WorkflowCategory" + code]))];

    public ObservableCollection<ReportTemplate> Templates { get; } = [];
    public ObservableCollection<ReportChoice> Columns { get; } = [];
    public ReportChoice? SelectedColumn { get => selectedColumn; set => Set(ref selectedColumn, value); }
    public ReportChoice? AvailableColumn { get => availableColumn; set => Set(ref availableColumn, value); }

    public ReportTemplate? SelectedTemplate
    {
        get => selectedTemplate;
        set
        {
            if (!Set(ref selectedTemplate, value)) return;
            Notify(nameof(CanChooseSource));
            if (value is null) return;
            Name = value.Name;
            ApplyLayout(value.Layout);
        }
    }

    public ReportChoice? SelectedSource
    {
        get => selectedSource;
        set
        {
            if (value is null || value == selectedSource) return;
            if (selectedTemplate is null) Name = value.Name;
            ApplyLayout(catalog.Single(s => s.Label.Code == value.Code).DefaultLayout);
        }
    }

    public ICommand ReloadCommand { get; }
    public ICommand NewCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand PreviewCommand { get; }
    public ICommand DownloadCommand { get; }
    public ICommand PrintCommand { get; }
    public ICommand AddColumnCommand { get; }
    public ICommand RemoveColumnCommand { get; }
    public ICommand MoveColumnCommand { get; }

    public Task LoadAsync(IReadOnlyList<ReportChoice>? statuses = null) => RunAsync(async () =>
    {
        var sources = await api.GetReportSourcesAsync();
        var templates = await api.GetReportTemplatesAsync();
        if (!isCurrent()) return;
        catalog = sources;
        Sources = catalog.Select(s => Choice(s.Label)).ToArray();
        if (statuses is not null) publishedStatuses = statuses;
        Notify(nameof(Sources));
        Templates.Clear();
        foreach (var template in templates) Templates.Add(template);
        loaded = true;
        ResetDraft();
        if (catalog.Count == 0) Message = T["ReportNoSources"];
    });

    private void ResetDraft()
    {
        SelectedTemplate = null;
        selectedSource = null;
        if (Sources.FirstOrDefault() is { } source) SelectedSource = source;
        else
        {
            Name = "";
            Note = null;
            Columns.Clear();
            Fields = SortOptions = GroupOptions = [];
            foreach (string property in new[] { nameof(SelectedSource), nameof(Fields), nameof(SortOptions), nameof(GroupOptions), nameof(Note) })
                Notify(property);
        }
    }

    private void ApplyLayout(ReportLayout layout)
    {
        var source = catalog.Single(s => s.Label.Code == layout.Source);
        selectedSource = Sources.Single(s => s.Code == layout.Source);
        Fields = source.Fields.Select(Choice).ToArray();
        SortOptions = [Any(), .. Fields];
        GroupOptions = [Any(), .. Fields.Where(f => source.Groups.Contains(f.Code))];
        StatusOptions = [Any(), .. publishedStatuses];
        if (layout.Status is { } status && StatusOptions.All(option => option.Code != status))
            StatusOptions = [.. StatusOptions, new ReportChoice(status, status)];
        Notify(nameof(StatusOptions));
        Columns.Clear();
        foreach (string code in layout.Columns) Columns.Add(Fields.Single(f => f.Code == code));
        SelectedColumn = Columns.FirstOrDefault();
        AvailableColumn = Fields.FirstOrDefault();
        ShowSummary = layout.ShowSummary;
        ShowTable = layout.ShowTable;
        Descending = layout.Descending;
        Landscape = layout.Landscape;
        Note = layout.Note;
        From = layout.DefaultFrom?.ToDateTime(TimeOnly.MinValue) ?? initialFrom;
        To = layout.DefaultTo?.ToDateTime(TimeOnly.MinValue) ?? initialTo;
        foreach (string property in new[] { nameof(SelectedSource), nameof(Fields), nameof(SortOptions), nameof(GroupOptions),
            nameof(UsesPeriod), nameof(IsOrderSource), nameof(CanEditLayout), nameof(ShowSummary), nameof(Descending), nameof(Landscape), nameof(Note),
            nameof(From), nameof(To) }) Notify(property);

        SortBy = layout.SortBy;
        GroupBy = layout.GroupBy;
        Status = layout.Status;
        Category = layout.Category;
        foreach (string property in new[] { nameof(SortBy), nameof(GroupBy), nameof(Status), nameof(Category) }) Notify(property);
    }

    private async Task SaveAsync()
    {
        var current = selectedTemplate;
        var saved = await api.SaveReportTemplateAsync(current?.Id, new(Name, BuildLayout(), current?.Version));
        if (!isCurrent()) return;
        int index = current is null ? -1 : Templates.IndexOf(current);
        if (index < 0) Templates.Add(saved);
        else Templates[index] = saved;
        SelectedTemplate = saved;
        Message = T["ReportTemplateSaved"];
    }

    private async Task GenerateAsync(ReportPdfAction action)
    {
        bool preview = action == ReportPdfAction.Preview;
        var from = Day(From);
        var to = Day(To);
        if (UsesPeriod && (from is null || to is null || from > to))
            throw new InvalidOperationException(T["InvalidReportRange"]);
        string language = T.LanguageCode;
        var request = selectedTemplate is { } saved && (action == ReportPdfAction.Save || !CanEdit)
            ? new GenerateReportPdf(TemplateId: saved.Id, ExpectedVersion: saved.Version, From: from, To: to, Language: language)
            : CanEdit
                ? new GenerateReportPdf(Draft: BuildLayout(),
                    Title: preview && string.IsNullOrWhiteSpace(Name) ? selectedSource!.Name : Name,
                    From: from, To: to, Language: language)
                : new GenerateReportPdf(Preset: selectedSource!.Code, From: from, To: to, Language: language);
        var result = preview ? await api.PreviewReportPdfAsync(request) : await api.GenerateReportPdfAsync(request);
        if (isCurrent() && await deliver(result, action) && isCurrent() && !preview)
            Message = T[action == ReportPdfAction.Print ? "PdfPrinted" : "PdfSaved"];
    }

    private ReportLayout BuildLayout() => new(selectedSource!.Code!, Columns.Select(column => column.Code!).ToArray(),
        SortBy, Descending, ShowTable ? GroupBy : null, ShowSummary, ShowTable, Note, Landscape,
        IsOrderSource ? Status : null, IsOrderSource ? Category : null, Day(From), Day(To));

    private DateOnly? Day(DateTime? date) => UsesPeriod && date is { } value ? DateOnly.FromDateTime(value) : null;
    private ReportChoice Any() => new(null, T["All"]);
    private ReportChoice Choice(ReportLabel label) => new(label.Code, label.In(T.LanguageCode));
    private static int Offset(object? direction) => direction is "up" ? -1 : 1;

    private async Task RunAsync(Func<Task> action)
    {
        if (busy || !isCurrent()) return;
        busy = true;
        Message = Error = null;
        NotifyReady();
        try { await action(); }
        catch (Exception ex) { if (isCurrent()) Error = ex.Message; }
        finally
        {
            busy = false;
            NotifyReady();
        }
    }

    private void NotifyReady()
    {
        Notify(nameof(IsReady));
        Notify(nameof(CanEditLayout));
        Notify(nameof(CanChooseSource));
        CommandManager.InvalidateRequerySuggested();
    }

    private bool Set<TValue>(ref TValue field, TValue value, [CallerMemberName] string? property = null)
    {
        if (EqualityComparer<TValue>.Default.Equals(field, value)) return false;
        field = value;
        Notify(property);
        CommandManager.InvalidateRequerySuggested();
        return true;
    }

    private void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed record ReportChoice(string? Code, string Name);

public enum ReportPdfAction { Preview, Save, Print }
