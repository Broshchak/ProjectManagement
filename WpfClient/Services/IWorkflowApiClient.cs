using Orders.Workflow;
using WpfClient.Models;

namespace WpfClient.Services;

public interface IWorkflowApiClient
{
    Task<WorkflowCatalog> GetWorkflowAsync();
    Task<WorkflowDraft> GetWorkflowDraftAsync();
    Task<IReadOnlyList<WorkflowOption>> GetWorkflowRulesAsync();
    Task<IReadOnlyList<WorkflowOption>> GetWorkflowRolesAsync();
    Task<WorkflowDraft> SaveWorkflowDraftAsync(SaveWorkflowDraft request);
    Task<WorkflowValidation> ValidateWorkflowDraftAsync(PublishWorkflow request);
    Task<WorkflowDraft> PublishWorkflowAsync(PublishWorkflow request);
    Task<OrderWorkflowActions> GetOrderTransitionsAsync(int orderId);
    Task<OrderDetail> ExecuteOrderTransitionAsync(int orderId, string code, ExecuteWorkflowTransition request);
}
