using System.Net;
using System.Windows.Input;
using Orders.Workflow;
using WpfClient.Models;
using WpfClient.Services;

namespace WpfClient.ViewModels;

public sealed partial class MainViewModel
{
    private readonly WorkflowEditorViewModel? _workflowEditor;
    private OrderWorkflowActions? _workflowActions;
    private string? _transitionComment;
    private IReadOnlyList<WorkflowAction> _availableActions = [];

    public IReadOnlyList<WorkflowAction> AvailableActions => _availableActions;
    public bool HasAvailableOrderStatusActions => AvailableActions.Count > 0;

    internal bool ConfirmDiscardWorkflow() => _workflowEditor?.ConfirmDiscard() != false;

    public string? TransitionComment
    {
        get => _transitionComment;
        set
        {
            _transitionComment = value;
            OnPropertyChanged();
        }
    }

    private async Task LoadWorkflowAsync(int session)
    {
        if (_permissions is null || !Can("orders.read") || _apiClient is not IWorkflowApiClient api) return;
        var catalog = await api.GetWorkflowAsync();
        if (session != _sessionVersion || !IsAuthenticated) return;
        _boardWorkflowRevision = catalog.Revision;
        if (!KanbanColumns.Select(c => (c.Status, c.Name, c.Color)).SequenceEqual(
            catalog.States.Select(s => (s.Code, (string?)s.Name, StateColor(s)))))
        {
            KanbanColumns.Clear();
            foreach (var state in catalog.States)
                KanbanColumns.Add(new() { Status = state.Code, Name = state.Name, Color = StateColor(state) });
            UpdateOrderStatusFilterOptions();
        }
    }

    private static string StateColor(WorkflowState state) =>
        string.IsNullOrEmpty(state.Color) ? "#255C99" : state.Color;

    private async Task LoadWorkflowActionsAsync(OrderDetail? details, int session, int version)
    {
        if (details is null || details.IsStatusFinal) return;
        if (_apiClient is IWorkflowApiClient api && _permissions is not null)
        {
            var actions = await api.GetOrderTransitionsAsync(details.Id);
            if (session != _sessionVersion || version != _detailVersion || actions.OrderVersion != details.Version) return;
            _workflowActions = actions;
        }
        else if (IsMockMode && CanManageOrders)
        {
            var actions = details.Status switch
            {
                "NewOrder" => new[] { "register", "cancel" },
                "Registered" => ["grant", "cancel"],
                "Granted" => ["ship", "cancel"],
                "Shipped" => ["invoice"],
                _ => []
            };
            _workflowActions = new(0, details.Version, actions.Select(code => new WorkflowAction(code, code, "", false)).ToArray());
        }
        RefreshWorkflowActionLabels();
    }

    private void ClearWorkflowActions()
    {
        _workflowActions = null;
        TransitionComment = null;
        RefreshWorkflowActionLabels();
    }

    private void RefreshWorkflowActionLabels()
    {
        _availableActions = _workflowActions?.Actions.Select(LocalizeAction).ToArray() ?? [];
        OnPropertyChanged(nameof(AvailableActions));
        OnOrderStatusActionPropertiesChanged();
    }

    private WorkflowAction LocalizeAction(WorkflowAction action) => action with
    {
        Name = (action.Code, action.Name) switch
        {
            ("register", "Register" or "register") => T["Register"],
            ("grant", "Approve" or "grant") => T["Grant"],
            ("ship", "Ship" or "ship") => T["Ship"],
            ("invoice", "Invoice" or "invoice") => T["Invoice"],
            ("cancel", "Cancel" or "cancel") => T["Cancel"],
            _ => action.Name
        }
    };

    private async Task ChangeOrderStatusAsync(WorkflowAction? action)
    {
        if (!CanExecuteOrderStatusAction(action) || SelectedOrderDetails is not { } details || _workflowActions is not { } actions)
            return;
        var selectedAction = action!;
        if (selectedAction.RequiresComment && string.IsNullOrWhiteSpace(TransitionComment))
        {
            ApiErrorMessage = T["WorkflowCommentRequired"];
            return;
        }

        int session = _sessionVersion;
        IsOrderStatusActionRunning = true;
        ApiErrorMessage = null;
        try
        {
            if (_apiClient is IWorkflowApiClient api)
                await api.ExecuteOrderTransitionAsync(details.Id, selectedAction.Code,
                    new(actions.WorkflowRevision, actions.OrderVersion, TransitionComment));
            else
                await _apiClient.ChangeOrderStatusAsync(details.Id, selectedAction.Code);

            if (session == _sessionVersion) await LoadClientDataAsync();
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Conflict && session == _sessionVersion)
        {
            await LoadClientDataAsync();
            if (session == _sessionVersion) ApiErrorMessage = T["WorkflowOrderConflict"];
        }
        catch (Exception ex)
        {
            if (session == _sessionVersion) ApiErrorMessage = T["WorkflowTransitionFailed"] + " " + ex.Message;
        }
        finally
        {
            IsOrderStatusActionRunning = false;
        }
    }

    private bool CanExecuteOrderStatusAction(WorkflowAction? action) =>
        action is not null && Can("orders.read", CanManageOrders) && !IsOrderStatusActionRunning && !IsLoading &&
        !IsOrderDetailsLoading && AvailableActions.Contains(action);

    private void OnOrderStatusActionPropertiesChanged()
    {
        OnPropertyChanged(nameof(HasAvailableOrderStatusActions));
        CommandManager.InvalidateRequerySuggested();
    }
}
