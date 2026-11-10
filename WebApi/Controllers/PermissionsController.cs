using Application.Dtos.Roles;
using Application.Security;
using Microsoft.AspNetCore.Mvc;
using WebApi.Security;

namespace WebApi.Controllers;

[ApiController]
[Route("api/permissions")]
[HasPermission(Permissions.RolesManageVisibility)]
public sealed class PermissionsController : ControllerBase
{
    [HttpGet]
    public IReadOnlyList<PermissionOptionDto> Catalog() => VisibilityCatalog.Options;
}
