using Application.Common.Interfaces;
using Application.Security;
using Application.Services;
using Microsoft.AspNetCore.Mvc;
using Orders.Workflow;
using WebApi.Security;

namespace WebApi.Controllers;

[ApiController]
[Route("api/workflow")]
public sealed class WorkflowController(OrderWorkflowService workflow, IPermissionProvider permissions) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var role = User.FindFirst(AppClaims.Role)!.Value;
        if (!await permissions.HasPermissionAsync(role, Permissions.OrdersRead, ct) &&
            !await permissions.HasPermissionAsync(role, Permissions.WorkflowManage, ct))
            return Forbid();
        return Ok(await workflow.GetCatalogAsync(ct));
    }

    [HttpGet("rule-types")]
    [HasPermission(Permissions.WorkflowManage)]
    public IActionResult GetRules() => Ok(OrderWorkflowService.Rules);

    [HttpGet("roles")]
    [HasPermission(Permissions.WorkflowManage)]
    public async Task<IActionResult> GetRoles(CancellationToken ct) => Ok(await workflow.GetRolesAsync(ct));

    [HttpGet("draft")]
    [HasPermission(Permissions.WorkflowManage)]
    public async Task<IActionResult> GetDraft(CancellationToken ct) => Ok(await workflow.GetDraftAsync(ct));

    [HttpPut("draft")]
    [HasPermission(Permissions.WorkflowManage)]
    public async Task<IActionResult> SaveDraft(SaveWorkflowDraft request, CancellationToken ct) =>
        Ok(await workflow.SaveDraftAsync(request, ct));

    [HttpPost("draft/validate")]
    [HasPermission(Permissions.WorkflowManage)]
    public async Task<IActionResult> ValidateDraft([FromBody] PublishWorkflow request, CancellationToken ct) =>
        Ok(await workflow.ValidateDraftAsync(request.ExpectedDraftVersion, ct));

    [HttpPost("draft/publish")]
    [HasPermission(Permissions.WorkflowManage)]
    public async Task<IActionResult> PublishDraft(PublishWorkflow request, CancellationToken ct) =>
        Ok(await workflow.PublishAsync(request, ct));
}
