using Orders.Workflow;

namespace WpfClient.Models;

public sealed record KanbanMove(int OrderId, string SourceStatus, long WorkflowRevision, string OrderVersion,
    IReadOnlyList<WorkflowAction> Actions, int Session);
