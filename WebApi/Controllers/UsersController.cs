using Application.Common.Interfaces;
using Application.Dtos.Users;
using Application.Security;
using Microsoft.AspNetCore.Mvc;
using WebApi.Security;

namespace WebApi.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController(IUserManagementService users) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.UsersRead)]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> GetUsers(CancellationToken ct) => Ok(await users.GetAllAsync(ct));

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.UsersRead)]
    public async Task<ActionResult<UserDto>> GetUser(int id, CancellationToken ct) =>
        await users.GetAsync(id, ct) is { } user ? Ok(user) : NotFound();

    [HttpPost]
    [HasPermission(Permissions.UsersManage)]
    public async Task<ActionResult<UserDto>> Create(CreateUserDto request, CancellationToken ct)
    {
        var user = (await users.SaveAsync(null, request, request.Password, ct))!;
        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.UsersManage)]
    public async Task<ActionResult<UserDto>> Update(int id, SaveUserDto request, CancellationToken ct) =>
        await users.SaveAsync(id, request, null, ct) is { } user ? Ok(user) : NotFound();

    [HttpPut("{id:int}/password")]
    [HasPermission(Permissions.UsersManage)]
    public async Task<IActionResult> ChangePassword(int id, ChangeUserPasswordDto request, CancellationToken ct) =>
        await users.ChangePasswordAsync(id, request.Password, ct) ? NoContent() : NotFound();
}
