using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Dtos.Orders;
using Application.Dtos.Products;
using Application.Security;
using Domain.Entities;
using Orders.Workflow;

namespace Application.Services;

public sealed class OrderWorkflowService(IOrderWorkflowRepository workflows, IOrderRepository orders,
    IRoleRepository roles, IProductRepository products, IPermissionProvider permissions, IUnitOfWork unitOfWork)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<WorkflowOption> Rules { get; } =
    [
        new("HasItems", "Order has items"),
        new("ProductsActive", "Products are active"),
        new("StockAvailable", "Stock is available"),
        new("CustomerPhoneRequired", "Customer phone is required"),
        new("CommentRequired", "Transition comment is required")
    ];

    public async Task<WorkflowCatalog> GetCatalogAsync(CancellationToken ct)
    {
        var row = await workflows.ReadAsync(ct);
        var definition = Read(row.Published);
        return new(row.Revision, definition.States.Where(s => s.IsActive).OrderBy(s => s.SortOrder).ToArray(),
            definition.Transitions.Where(t => t.IsActive).Select(t => new WorkflowEdge(t.Code, t.Name, t.From, t.To)).ToArray());
    }

    public async Task<IReadOnlyList<WorkflowOption>> GetRolesAsync(CancellationToken ct) =>
        (await roles.ListAsync(ct)).Where(r => r.Code is not (Roles.TelegramBot or Roles.Viewer))
            .Select(r => new WorkflowOption(r.Code, r.Name)).ToArray();

    public async Task<WorkflowDraft> GetDraftAsync(CancellationToken ct) => MapDraft(await workflows.ReadAsync(ct));

    public Task<WorkflowDraft> SaveDraftAsync(SaveWorkflowDraft request, CancellationToken ct)
    {
        if (request.Definition?.States is null || request.Definition.Transitions is null ||
            request.Definition.States.Count > 100 || request.Definition.Transitions.Count > 300)
            throw new ValidationException("A workflow supports up to 100 states and 300 transitions.");
        if (request.Definition.States.Any(s => s is null) ||
            request.Definition.Transitions.Any(t => t is null || t.Roles is null || t.Rules is null))
            throw new ValidationException("States and transitions must have valid objects and lists.");
        return unitOfWork.InTransactionAsync(async token =>
        {
            var row = await workflows.LockAsync(true, token);
            CheckDraftVersion(row, request.ExpectedDraftVersion);
            row.Draft = JsonSerializer.Serialize(request.Definition, JsonOptions);
            row.DraftVersion++;
            await unitOfWork.SaveChangesAsync(token);
            return MapDraft(row);
        }, ct);
    }

    public Task<WorkflowValidation> ValidateDraftAsync(long expectedVersion, CancellationToken ct) =>
        unitOfWork.InTransactionAsync(async token =>
        {
            var row = await workflows.LockAsync(false, token);
            CheckDraftVersion(row, expectedVersion);
            return await ValidateAsync(Read(row.Draft), token);
        }, ct);

    public Task<WorkflowDraft> PublishAsync(PublishWorkflow request, CancellationToken ct) =>
        unitOfWork.InTransactionAsync(async token =>
        {
            var row = await workflows.LockAsync(true, token);
            CheckDraftVersion(row, request.ExpectedDraftVersion);
            if (row.Revision != request.ExpectedWorkflowRevision)
                throw new DataConflictException("The published workflow changed. Reload the draft.");
            var definition = Read(row.Draft);
            var validation = await ValidateAsync(definition, token);
            if (!validation.IsValid)
                throw new ValidationException(string.Join(" ", validation.Errors));

            await workflows.ApplyStatesAsync(definition.States, token);
            row.Published = row.Draft;
            row.Revision++;
            row.DraftVersion++;
            await unitOfWork.SaveChangesAsync(token);
            return MapDraft(row);
        }, ct);

    public async Task<string> GetInitialStateAsync(CancellationToken ct)
    {
        var row = await workflows.LockAsync(false, ct);
        return Read(row.Published).States.Single(s => s.IsActive && s.IsInitial).Code;
    }

    public async Task<bool> ReferencesRoleAsync(string code, CancellationToken ct)
    {
        var row = await workflows.LockAsync(false, ct);
        return Read(row.Published).Transitions.Concat(Read(row.Draft).Transitions)
            .Any(transition => transition?.Roles?.Contains(code) == true);
    }

    public Task<OrderWorkflowActions?> GetActionsAsync(int orderId, string role, CancellationToken ct) =>
        unitOfWork.InTransactionAsync<OrderWorkflowActions?>(async token =>
        {
            var row = await workflows.LockAsync(false, token);
            var order = await orders.GetDetailByIdAsync(orderId, token);
            if (order is null) return null;
            bool canEdit = await permissions.HasPermissionAsync(role, Permissions.OrdersEdit, token);
            var candidates = Allowed(Read(row.Published), order, role, canEdit).ToArray();
            var availability = await GetRuleAvailabilityAsync(order, candidates, token);
            var actions = candidates.Where(t => RuleViolation(t, order, availability) is null)
                .Select(t => new WorkflowAction(t.Code, t.Name, t.To, t.Rules.Contains("CommentRequired"))).ToArray();
            return new(row.Revision, order.Version, actions, order.StatusCode);
        }, ct);

    public Task<OrderDetailDto?> ExecuteAsync(int orderId, string action, int userId, string role,
        ExecuteWorkflowTransition? request, CancellationToken ct) =>
        unitOfWork.InTransactionAsync(async token =>
        {
            var row = await workflows.LockAsync(false, token);
            var order = await orders.GetForUpdateAsync(orderId, token);
            if (order is null) return null;

            var current = (await orders.GetDetailByIdAsync(orderId, token))!;
            if (request is not null && (row.Revision != request.ExpectedWorkflowRevision || current.Version != request.ExpectedVersion))
                throw new DataConflictException("The order or workflow changed. Reload it before moving the order.");
            var transition = current.IsStatusFinal ? null : Read(row.Published).Transitions.SingleOrDefault(t =>
                t.IsActive && t.From == current.StatusCode && string.Equals(t.Code, action?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (transition is null)
                throw new ValidationException("This transition is unavailable for the current state.");
            bool canEdit = await permissions.HasPermissionAsync(role, Permissions.OrdersEdit, token);
            if (!RoleCanExecute(transition, role, canEdit))
                throw new WorkflowAccessException();

            var comment = request?.Comment?.Trim();
            if (comment?.Length > 1000)
                throw new ValidationException("The transition comment must be at most 1000 characters.");
            if (transition.Rules.Contains("CommentRequired") && string.IsNullOrWhiteSpace(comment))
                throw new ValidationException("A transition comment is required.");
            var availability = await GetRuleAvailabilityAsync(current, [transition], token);
            if (RuleViolation(transition, current, availability) is { } violation)
                throw new ValidationException(violation);
            var target = await orders.GetStatusByCodeAsync(transition.To, token)
                ?? throw new InvalidOperationException("The workflow target state was not found.");

            await orders.AddStatusHistoryAsync(new OrderStatusHistoryEntity
            {
                OrderId = order.Id,
                PreviousStatusId = order.StatusId,
                NewStatusId = target.Id,
                ChangedByUserId = userId,
                Comment = string.IsNullOrWhiteSpace(comment) ? transition.Name : comment
            }, token);
            order.StatusId = target.Id;
            await unitOfWork.SaveChangesAsync(token);
            return await orders.GetDetailByIdAsync(orderId, token);
        }, ct);

    private static IEnumerable<WorkflowTransition> Allowed(WorkflowDefinition definition, OrderDetailDto order, string role, bool canEdit) =>
        order.IsStatusFinal ? [] : definition.Transitions.Where(t =>
            t.IsActive && t.From == order.StatusCode && RoleCanExecute(t, role, canEdit));

    private static bool RoleCanExecute(WorkflowTransition transition, string role, bool canEdit) =>
        transition.Roles.Count == 0 ? canEdit : transition.Roles.Contains(role);

    private async Task<IReadOnlyDictionary<int, ProductAvailabilityDto>> GetRuleAvailabilityAsync(
        OrderDetailDto order, IReadOnlyCollection<WorkflowTransition> transitions, CancellationToken ct)
    {
        if (!transitions.Any(t => t.Rules.Contains("ProductsActive") || t.Rules.Contains("StockAvailable")))
            return new Dictionary<int, ProductAvailabilityDto>();
        return (await products.GetAvailabilityAsync(order.Items.Select(i => i.ProductId).ToArray(), ct))
            .ToDictionary(product => product.ProductId);
    }

    private static string? RuleViolation(WorkflowTransition transition, OrderDetailDto order,
        IReadOnlyDictionary<int, ProductAvailabilityDto> availability)
    {
        if (order.Items.Any(i => i.Quantity <= 0))
            return "Order item quantities must be positive.";
        var rules = transition.Rules;
        if (rules.Contains("HasItems") && order.Items.Count == 0)
            return "Add at least one item before this transition.";
        if (rules.Contains("CustomerPhoneRequired") && string.IsNullOrWhiteSpace(order.CustomerPhone))
            return "A customer phone number is required.";
        foreach (var item in order.Items)
        {
            availability.TryGetValue(item.ProductId, out var product);
            if (rules.Contains("ProductsActive") && product?.IsActive != true)
                return $"Product {item.ProductId} is unavailable.";
            if (rules.Contains("StockAvailable") && (product?.Stock is not int stock || item.Quantity > stock))
                return $"Insufficient stock for product {item.ProductId}.";
        }
        return null;
    }

    private async Task<WorkflowValidation> ValidateAsync(WorkflowDefinition draft, CancellationToken ct)
    {
        List<string> errors = [];
        Dictionary<string, WorkflowState> states = new(StringComparer.OrdinalIgnoreCase);
        var positions = new HashSet<short>();
        foreach (var state in draft.States)
        {
            if (state is null || !ValidCode(state.Code) || string.IsNullOrWhiteSpace(state.Name) || state.Name.Length > 80)
            {
                errors.Add("Every state needs a valid code and a name of up to 80 characters.");
                continue;
            }
            if (!states.TryAdd(state.Code, state))
                errors.Add($"Duplicate state code: {state.Code}.");
            if (state.SortOrder <= 0 || (state.IsActive && !positions.Add(state.SortOrder)))
                errors.Add($"State {state.Code} needs a unique positive position.");
            if (state.Category is not ("New" or "InProgress" or "Shipped" or "Completed" or "Cancelled"))
                errors.Add($"State {state.Code} has an unsupported category.");
            if (state.IsFinal && (state.CanEditItems || state.IsInitial))
                errors.Add($"Final state {state.Code} cannot be initial or allow editing.");
            if (state.Color is { Length: > 0 } color &&
                (color.Length != 7 || color[0] != '#' || !color.Skip(1).All(Uri.IsHexDigit)))
                errors.Add($"State {state.Code} needs a color in #RRGGBB format.");
        }

        var activeStates = states.Values.Where(s => s.IsActive).ToArray();
        var initial = activeStates.Where(s => s.IsInitial).ToArray();
        if (initial.Length != 1)
            errors.Add("Select exactly one initial state.");

        var roleCodes = (await GetRolesAsync(ct)).Select(r => r.Code).ToHashSet(StringComparer.Ordinal);
        var ruleCodes = Rules.Select(r => r.Code).ToHashSet(StringComparer.Ordinal);
        var keys = new HashSet<(string, string)>();
        List<WorkflowTransition> edges = [];
        foreach (var transition in draft.Transitions)
        {
            if (transition is null || !ValidCode(transition.Code) || string.IsNullOrWhiteSpace(transition.Name) ||
                transition.Name.Length > 80 || transition.From is null || transition.To is null ||
                !states.TryGetValue(transition.From, out var from) || from.Code != transition.From ||
                !states.TryGetValue(transition.To, out var to) || to.Code != transition.To ||
                from.Code == to.Code || (transition.IsActive && (from.IsFinal || !from.IsActive || !to.IsActive)))
            {
                errors.Add("Every transition needs a code, a name and two different existing states. Final states cannot have outgoing transitions.");
                continue;
            }
            if (!keys.Add((transition.From, transition.Code.ToUpperInvariant())))
                errors.Add($"Duplicate transition {transition.Code} from {transition.From}.");
            if (transition.Roles is null || transition.Roles.Any(role => !roleCodes.Contains(role)))
                errors.Add($"Transition {transition.Code} has an unknown role.");
            if (transition.Rules is null || transition.Rules.Any(rule => !ruleCodes.Contains(rule)))
                errors.Add($"Transition {transition.Code} has an unknown rule.");
            if (transition.IsActive) edges.Add(transition);
        }

        if (initial.Length == 1)
        {
            var reachable = Reachable([initial[0].Code], edges.Select(t => (t.From, t.To)));
            var canFinish = Reachable(activeStates.Where(s => s.IsFinal).Select(s => s.Code), edges.Select(t => (t.To, t.From)));
            foreach (var state in activeStates)
            {
                if (!reachable.Contains(state.Code)) errors.Add($"State {state.Code} is unreachable.");
                if (!canFinish.Contains(state.Code)) errors.Add($"State {state.Code} has no path to a final state.");
            }
        }

        var usage = await workflows.GetStatusUsageAsync(ct);
        foreach (var state in usage.Where(s => s.OrderCount > 0 || s.HasHistory))
        {
            if (!states.TryGetValue(state.Code, out var replacement) || replacement.Code != state.Code)
                errors.Add($"State {state.Code} is used by orders or history and cannot be removed or renamed.");
            else if (state.OrderCount > 0 && !replacement.IsActive)
                errors.Add($"State {state.Code} has current orders and cannot be disabled.");
        }

        return new(errors, usage.Sum(s => s.OrderCount));
    }

    private static HashSet<string> Reachable(IEnumerable<string> start, IEnumerable<(string From, string To)> edges)
    {
        var targets = edges.ToLookup(e => e.From, e => e.To);
        var visited = start.ToHashSet();
        Queue<string> pending = new(visited);
        while (pending.TryDequeue(out var state))
            foreach (var target in targets[state])
                if (visited.Add(target)) pending.Enqueue(target);
        return visited;
    }

    private static bool ValidCode(string? code) => code is { Length: > 0 and <= 30 } &&
        char.IsAsciiLetter(code[0]) && code.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-');

    private static WorkflowDefinition Read(string json) =>
        JsonSerializer.Deserialize<WorkflowDefinition>(json, JsonOptions)!;

    private static WorkflowDraft MapDraft(OrderWorkflowEntity row) =>
        new(row.DraftVersion, row.Revision, Read(row.Draft));

    private static void CheckDraftVersion(OrderWorkflowEntity row, long expected)
    {
        if (row.DraftVersion != expected)
            throw new DataConflictException("The draft changed. Reload it before continuing.");
    }
}
