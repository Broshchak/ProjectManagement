using Application.Common.Interfaces;
using Application.Dtos.Users;
using Microsoft.AspNetCore.Mvc;
using WebApi.Filters;

namespace WebApi.Controllers;

[ApiController]
[Route("api/users")]
[ServiceErrors]
public sealed class UsersController(IUserManagementService users) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> GetUsers(CancellationToken ct) =>
        Ok(await users.GetAllAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserDto>> GetUser(int id, CancellationToken ct)
    {
        var user = await users.GetAsync(id, ct);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create(CreateUserDto request, CancellationToken ct)
    {
        var user = (await users.SaveAsync(null, request, request.Password, ct))!;
        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<UserDto>> Update(int id, SaveUserDto request, CancellationToken ct)
    {
        var user = await users.SaveAsync(id, request, null, ct);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPut("{id:int}/password")]
    public async Task<IActionResult> ChangePassword(int id, ChangeUserPasswordDto request, CancellationToken ct) =>
        await users.ChangePasswordAsync(id, request.Password, ct) ? NoContent() : NotFound();
}