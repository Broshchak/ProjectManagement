using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Orders.Workflow;

namespace Infrastructure.Persistence.Repositories;

public sealed class OrderWorkflowRepository(AppDbContext context) : IOrderWorkflowRepository
{
    public Task<OrderWorkflowEntity> ReadAsync(CancellationToken ct) =>
        context.Set<OrderWorkflowEntity>().AsNoTracking().SingleAsync(ct);

    public Task<OrderWorkflowEntity> LockAsync(bool forUpdate, CancellationToken ct)
    {
        if (context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("A workflow lock requires an active transaction.");
        var entries = context.Set<OrderWorkflowEntity>();
        return forUpdate
            ? entries.FromSqlRaw("SELECT * FROM order_workflow WHERE id = 1 FOR UPDATE").SingleAsync(ct)
            : entries.FromSqlRaw("SELECT * FROM order_workflow WHERE id = 1 FOR SHARE").AsNoTracking().SingleAsync(ct);
    }

    public async Task<IReadOnlyList<WorkflowStatusUsage>> GetStatusUsageAsync(CancellationToken ct) =>
        await context.Set<OrderStatusEntity>().Select(s => new WorkflowStatusUsage(s.Code,
            context.Set<OrderEntity>().Count(o => o.StatusId == s.Id),
            context.Set<OrderStatusHistoryEntity>().Any(h => h.NewStatusId == s.Id || h.PreviousStatusId == s.Id)))
            .ToListAsync(ct);

    public async Task ApplyStatesAsync(IReadOnlyList<WorkflowState> states, CancellationToken ct)
    {
        var table = context.Set<OrderStatusEntity>();
        var existing = await table.ToDictionaryAsync(state => state.Code, ct);
        foreach (var state in states)
        {
            if (!existing.TryGetValue(state.Code, out var row))
                table.Add(row = new OrderStatusEntity { Code = state.Code });
            row.Name = state.Name;
            row.SortOrder = state.SortOrder;
            row.IsFinal = state.IsFinal;
            row.CanEditItems = state.CanEditItems;
            row.Category = state.Category;
        }
    }
}
