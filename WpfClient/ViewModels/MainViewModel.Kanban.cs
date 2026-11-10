using System.Net;
using System.Net.Http;
using System.Text.Json;
using Orders.Workflow;
using WpfClient.Models;
using WpfClient.Services;

namespace WpfClient.ViewModels;

public sealed partial class MainViewModel
{
    private readonly HashSet<int> _kanbanNeedsVerification = [];
    private KanbanMove? _preparedKanbanMove;
    private long _boardWorkflowRevision;
    private int _kanbanPreparationVersion;
    private string? _kanbanMoveMessage;

    public string? KanbanMoveMessage
    {
        get => _kanbanMoveMessage;
        private set
        {
            _kanbanMoveMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasKanbanMoveMessage));
        }
    }

    public bool HasKanbanMoveMessage => !string.IsNullOrWhiteSpace(KanbanMoveMessage);
    public bool CanUseKanbanMove => IsAuthenticated && Can("board.read") && Can("orders.read") && _apiClient is IWorkflowApiClient;

    public bool CanPrepareKanbanMove(OrderListItem order) =>
        CanUseKanbanMove && !IsLoading && !IsOrderStatusActionRunning && !order.IsStatusFinal && _allOrders.Contains(order);

    public async Task<KanbanMove?> PrepareKanbanMoveAsync(OrderListItem order)
    {
        if (!CanPrepareKanbanMove(order) || _apiClient is not IWorkflowApiClient api) return null;
        int session = _sessionVersion;
        int preparation = ++_kanbanPreparationVersion;
        _preparedKanbanMove = null;
        KanbanMoveMessage = T["KanbanChecking"];

        if (_kanbanNeedsVerification.Contains(order.Id))
        {
            await ReconcileKanbanOrderAsync(order.Id, session, T["KanbanUnknownResult"]);
            return null;
        }

        try
        {
            var snapshot = await api.GetOrderTransitionsAsync(order.Id);
            if (!IsCurrentPreparation()) return null;
            if (!CanPrepareKanbanMove(order))
            {
                KanbanMoveMessage = null;
                return null;
            }
            if (string.IsNullOrWhiteSpace(snapshot.StatusCode) || string.IsNullOrWhiteSpace(snapshot.OrderVersion))
                throw new InvalidOperationException(T["KanbanSnapshotUnavailable"]);
            if (snapshot.StatusCode != order.Status || snapshot.WorkflowRevision != _boardWorkflowRevision)
            {
                await LoadClientDataAsync();
                if (IsCurrentPreparation()) KanbanMoveMessage = T["WorkflowOrderConflict"];
                return null;
            }

            var actions = snapshot.Actions.Where(action => action.TargetStatus != order.Status &&
                KanbanColumns.Any(column => column.Status == action.TargetStatus)).Select(LocalizeAction).ToArray();
            KanbanMoveMessage = actions.Length == 0 ? T["KanbanNoTargets"] : null;
            if (actions.Length == 0) return null;
            return _preparedKanbanMove = new(order.Id, order.Status, snapshot.WorkflowRevision, snapshot.OrderVersion, actions, session);
        }
        catch (Exception ex)
        {
            if (IsCurrentPreparation()) KanbanMoveMessage = KanbanError(ex, "WorkflowActionsFailed");
            return null;
        }

        bool IsCurrentPreparation() => session == _sessionVersion && preparation == _kanbanPreparationVersion;
    }

    public bool CanDropKanbanMove(KanbanMove move, string status) =>
        ReferenceEquals(_preparedKanbanMove, move) && move.Session == _sessionVersion &&
        move.WorkflowRevision == _boardWorkflowRevision && CanUseKanbanMove &&
        !IsLoading && !IsOrderStatusActionRunning && move.SourceStatus != status &&
        _allOrders.Any(order => order.Id == move.OrderId && order.Status == move.SourceStatus) &&
        move.Actions.Any(action => action.TargetStatus == status) &&
        KanbanColumns.Any(column => column.Status == status);

    public void CancelKanbanMove(KanbanMove move)
    {
        if (ReferenceEquals(_preparedKanbanMove, move)) _preparedKanbanMove = null;
    }

    public async Task ExecuteKanbanMoveAsync(KanbanMove move, string status, WorkflowAction action, string? comment)
    {
        if (!CanDropKanbanMove(move, status) || !move.Actions.Contains(action) || action.TargetStatus != status ||
            _apiClient is not IWorkflowApiClient api) return;
        _preparedKanbanMove = null;
        if (action.RequiresComment && string.IsNullOrWhiteSpace(comment))
        {
            KanbanMoveMessage = T["WorkflowCommentRequired"];
            return;
        }

        IsOrderStatusActionRunning = true;
        KanbanMoveMessage = T["KanbanMoving"];
        try
        {
            var order = await api.ExecuteOrderTransitionAsync(move.OrderId, action.Code,
                new(move.WorkflowRevision, move.OrderVersion, comment));
            if (move.Session != _sessionVersion) return;
            if (order.Id != move.OrderId || order.Status != status || string.IsNullOrWhiteSpace(order.Version))
            {
                await ReconcileKanbanOrderAsync(move.OrderId, move.Session, T["KanbanUnknownResult"]);
                return;
            }
            ApplyConfirmedKanbanOrder(move.OrderId, order);
            KanbanMoveMessage = T.Format("KanbanMoveConfirmed", order.StatusText);
            await LoadClientDataAsync();
        }
        catch (ApiException ex) when (ex.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.NotFound)
        {
            await ReconcileKanbanOrderAsync(move.OrderId, move.Session, T["KanbanConflict"]);
        }
        catch (ApiException ex) when ((int?)ex.StatusCode >= 500)
        {
            await ReconcileKanbanOrderAsync(move.OrderId, move.Session, T["KanbanUnknownResult"]);
        }
        catch (ApiException ex)
        {
            if (move.Session == _sessionVersion) KanbanMoveMessage = KanbanError(ex, "WorkflowTransitionFailed");
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException)
        {
            await ReconcileKanbanOrderAsync(move.OrderId, move.Session, T["KanbanUnknownResult"]);
        }
        finally
        {
            IsOrderStatusActionRunning = false;
        }
    }

    private async Task ReconcileKanbanOrderAsync(int id, int session, string reason)
    {
        if (session != _sessionVersion || !CanUseKanbanMove) return;
        _kanbanNeedsVerification.Add(id);
        try
        {
            var order = await _apiClient.GetOrderDetailsAsync(id);
            if (session != _sessionVersion) return;
            if (order is not null && (order.Id != id || string.IsNullOrWhiteSpace(order.Version) || string.IsNullOrWhiteSpace(order.Status)))
                throw new InvalidOperationException(T["KanbanSnapshotUnavailable"]);

            _kanbanNeedsVerification.Remove(id);
            ApplyConfirmedKanbanOrder(id, order);
            KanbanMoveMessage = order is null ? T["KanbanOrderMissing"] : reason + " " + T.Format("KanbanCurrentStatus", order.StatusText);
            await LoadClientDataAsync();
        }
        catch (Exception ex)
        {
            if (session == _sessionVersion) KanbanMoveMessage = reason + " " + KanbanError(ex, "KanbanVerifyFailed");
        }
    }

    private void ApplyConfirmedKanbanOrder(int id, OrderDetail? details)
    {
        var order = _allOrders.FirstOrDefault(order => order.Id == id);
        if (order is null) return;
        if (details is null)
        {
            _allOrders.Remove(order);
        }
        else
        {
            order.Status = details.Status;
            order.StatusName = details.StatusName;
            order.StatusCategory = details.StatusCategory;
            order.IsStatusFinal = details.IsStatusFinal;
            order.Total = details.Total;
        }

        ApplyOrderFilters();
        ApplyKanbanFilters();
        Summary = BuildReportSummary(_allOrders, _allProducts);
        InvalidateReports();
        if (SelectedOrder?.Id == id)
        {
            _detailVersion++;
            IsOrderDetailsLoading = false;
            ClearWorkflowActions();
            SelectedOrderDetails = details;
            SelectedOrderItem = details?.Items.FirstOrDefault();
        }
    }

    private string KanbanError(Exception error, string key) =>
        error is ApiException { StatusCode: HttpStatusCode.Forbidden } ? T["KanbanAccessDenied"] : T[key] + " " + error.Message;

    private void ResetKanbanMove()
    {
        _kanbanPreparationVersion++;
        _boardWorkflowRevision = 0;
        _preparedKanbanMove = null;
        _kanbanNeedsVerification.Clear();
        KanbanMoveMessage = null;
    }
}
