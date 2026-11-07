using Application.Common.Interfaces;
using Application.Dtos.Auth;
using Application.Security;
using Microsoft.AspNetCore.Mvc;
using WebApi.Security;

namespace WebApi.Controllers
{
    [Route("api/users")]
    [ApiController]
    [HasPermission(Permissions.UsersManage)]
    public class UsersController(IUserService userService) : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateUserDto dto, CancellationToken ct)
        {
            var user = await userService.CreateUserAsync(dto, ct);
            return Created($"/api/users/{user.Id}", user);
        }
    }
}
