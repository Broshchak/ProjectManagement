using System.Security.Claims;
using Application.Common.Interfaces;
using Application.Dtos.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService auth) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<LoginResponseDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Login(LoginRequestDto request, CancellationToken ct)
    {
        var result = await auth.LoginAsync(request, ct);
        return result is null ? Unauthorized(new ProblemDetails { Title = "Invalid credentials." }) : Ok(result);
    }

    [HttpGet("me")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType<CurrentUserDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!int.TryParse(raw, out var userId))
            return Unauthorized();

        var me = await auth.GetCurrentUserAsync(userId, ct);
        return me is null ? Unauthorized() : Ok(me);
    }
}
