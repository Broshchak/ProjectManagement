using System.Net.Http;
using System.Net.Http.Json;
using Orders.Workflow;
using WpfClient.Models;

namespace WpfClient.Services;

public sealed partial class HttpOrdersApiClient
{
    public Task<WorkflowCatalog> GetWorkflowAsync() => GetWorkflowJsonAsync<WorkflowCatalog>("api/workflow");
    public Task<WorkflowDraft> GetWorkflowDraftAsync() => GetWorkflowJsonAsync<WorkflowDraft>("api/workflow/draft");
    public async Task<IReadOnlyList<WorkflowOption>> GetWorkflowRulesAsync() =>
        await GetWorkflowJsonAsync<WorkflowOption[]>("api/workflow/rule-types");
    public async Task<IReadOnlyList<WorkflowOption>> GetWorkflowRolesAsync() =>
        await GetWorkflowJsonAsync<WorkflowOption[]>("api/workflow/roles");

    public Task<WorkflowDraft> SaveWorkflowDraftAsync(SaveWorkflowDraft request) =>
        WriteWorkflowAsync<WorkflowDraft>(HttpMethod.Put, "api/workflow/draft", request);
    public Task<WorkflowValidation> ValidateWorkflowDraftAsync(PublishWorkflow request) =>
        WriteWorkflowAsync<WorkflowValidation>(HttpMethod.Post, "api/workflow/draft/validate", request);
    public Task<WorkflowDraft> PublishWorkflowAsync(PublishWorkflow request) =>
        WriteWorkflowAsync<WorkflowDraft>(HttpMethod.Post, "api/workflow/draft/publish", request);
    public Task<OrderWorkflowActions> GetOrderTransitionsAsync(int orderId) =>
        GetWorkflowJsonAsync<OrderWorkflowActions>($"api/orders/{orderId}/transitions");

    public async Task<OrderDetail> ExecuteOrderTransitionAsync(int orderId, string code, ExecuteWorkflowTransition request) =>
        MapOrderDetail(await WriteWorkflowAsync<OrderDetailResponse>(HttpMethod.Post,
            $"api/orders/{orderId}/transitions/{Uri.EscapeDataString(code)}", request));

    private async Task<T> GetWorkflowJsonAsync<T>(string path) where T : class =>
        await GetJsonAsync<T>(path, default) ?? throw new InvalidOperationException(LocalizationManager.Instance["WorkflowEmptyResponse"]);

    private async Task<T> WriteWorkflowAsync<T>(HttpMethod method, string path, object request) where T : class
    {
        using var response = await SendAsync(method, path, JsonContent.Create(request), default);
        return await response.Content.ReadFromJsonAsync<T>()
            ?? throw new InvalidOperationException(LocalizationManager.Instance["WorkflowEmptyResponse"]);
    }
}
