namespace Orders.Workflow;

public sealed record WorkflowState(string Code, string Name, short SortOrder, string Category,
    bool IsInitial, bool IsFinal, bool CanEditItems, string? Color = null, bool IsActive = true);

public sealed record WorkflowTransition(string Code, string Name, string From, string To,
    IReadOnlyList<string> Roles, IReadOnlyList<string> Rules, bool IsActive = true);

public sealed record WorkflowDefinition(IReadOnlyList<WorkflowState> States, IReadOnlyList<WorkflowTransition> Transitions);
public sealed record WorkflowEdge(string Code, string Name, string From, string To);
public sealed record WorkflowCatalog(long Revision, IReadOnlyList<WorkflowState> States, IReadOnlyList<WorkflowEdge> Transitions);
public sealed record WorkflowDraft(long Version, long PublishedRevision, WorkflowDefinition Definition);
public sealed record SaveWorkflowDraft(long ExpectedDraftVersion, WorkflowDefinition Definition);
public sealed record PublishWorkflow(long ExpectedDraftVersion, long ExpectedWorkflowRevision);
public sealed record WorkflowValidation(IReadOnlyList<string> Errors, int AffectedOrders)
{
    public bool IsValid => Errors.Count == 0;
}

public sealed record WorkflowOption(string Code, string Name);
public sealed record WorkflowAction(string Code, string Name, string TargetStatus, bool RequiresComment);
public sealed record OrderWorkflowActions(long WorkflowRevision, string OrderVersion, IReadOnlyList<WorkflowAction> Actions,
    string? StatusCode = null);
public sealed record ExecuteWorkflowTransition(long ExpectedWorkflowRevision, string ExpectedVersion, string? Comment);
