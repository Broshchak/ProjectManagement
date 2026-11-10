using Domain.Entities;
using Orders.Workflow;

namespace Application.Common.Interfaces;

public interface IOrderWorkflowRepository
{
    Task<OrderWorkflowEntity> ReadAsync(CancellationToken ct);
    Task<OrderWorkflowEntity> LockAsync(bool forUpdate, CancellationToken ct);
    Task<IReadOnlyList<WorkflowStatusUsage>> GetStatusUsageAsync(CancellationToken ct);
    Task ApplyStatesAsync(IReadOnlyList<WorkflowState> states, CancellationToken ct);
}

public sealed record WorkflowStatusUsage(string Code, int OrderCount, bool HasHistory);
