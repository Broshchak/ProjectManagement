using System.Globalization;
using Application.Dtos.Roles;
using Application.Security;
using Application.Services;
using Microsoft.AspNetCore.Mvc;
using WebApi.Security;

namespace WebApi.Controllers;

[ApiController]
[Route("api/roles")]
[HasPermission(Permissions.RolesManage)]
public sealed class RolesController(RoleService roles) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<RoleDto>> List(CancellationToken ct) => await roles.ListAsync(ct);

    [HttpGet("permissions")]
    [HasPermission(Permissions.RolesManageVisibility)]
    public async Task<IReadOnlyList<RoleVisibilityDto>> Visibility(CancellationToken ct) =>
        await roles.ListVisibilityAsync(ct);

    [HttpPut("{id:int}/permissions")]
    [HasPermission(Permissions.RolesManageVisibility)]
    public async Task<ActionResult<RoleVisibilityDto>> SaveVisibility(int id, SetRoleVisibilityDto request, CancellationToken ct) =>
        await roles.SaveVisibilityAsync(id, request, ct) is { } result ? Ok(result) : NotFound();

    [HttpGet("{id:int}")]
    public async Task<ActionResult<RoleDto>> Get(int id, CancellationToken ct) =>
        await roles.GetAsync(id, ct) is { } role ? Ok(role) : NotFound();

    [HttpPost]
    public async Task<ActionResult<RoleDto>> Create(CreateRoleDto request, CancellationToken ct)
    {
        var role = await roles.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = role.Id }, role);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<RoleDto>> Rename(int id, RenameRoleDto request, CancellationToken ct) =>
        await roles.RenameAsync(id, request, ct) is { } role ? Ok(role) : NotFound();

    [HttpPut("{id:int}/active")]
    public async Task<ActionResult<RoleDto>> SetActive(int id, SetRoleActiveDto request, CancellationToken ct) =>
        await roles.SetActiveAsync(id, request, ct) is { } role ? Ok(role) : NotFound();

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        string header = Request.Headers.IfMatch.ToString().Trim();
        if (header.Length == 0)
            return Problem(statusCode: StatusCodes.Status428PreconditionRequired, title: "If-Match is required.");
        if (header.Length < 3 || header[0] != '"' || header[^1] != '"' ||
            !long.TryParse(header.AsSpan(1, header.Length - 2), NumberStyles.None, CultureInfo.InvariantCulture, out long version) || version < 1)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "If-Match must contain the quoted role version.");
        return await roles.DeleteAsync(id, version, ct) ? NoContent() : NotFound();
    }
}
