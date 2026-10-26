using System.Globalization;
using Application.Common.Interfaces;
using Application.Dtos.Auth;
using Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace WebApi.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController(IAuthService authService, IPermissionProvider permissions) : ControllerBase
    {
        [HttpPost("login")]
        [AllowAnonymous]
        [EnableRateLimiting("login")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Login([FromBody] LoginDto dto, CancellationToken ct)
        {
            var tokens = await authService.LoginAsync(dto, ct);
            return tokens is null ? InvalidCredentials() : Ok(tokens);
        }

        [HttpPost("refresh")]
        [AllowAnonymous]
        [EnableRateLimiting("refresh")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenDto dto, CancellationToken ct)
        {
            var tokens = await authService.RefreshAsync(dto.RefreshToken, ct);
            return tokens is null ? InvalidCredentials() : Ok(tokens);
        }

        [HttpPost("logout")]
        [AllowAnonymous]
        [EnableRateLimiting("refresh")]
        public async Task<IActionResult> Logout([FromBody] RefreshTokenDto dto, CancellationToken ct)
        {
            await authService.LogoutAsync(dto.RefreshToken, ct);
            return NoContent();
        }

        [HttpGet("me")]
        public async Task<IActionResult> Me(CancellationToken ct)
        {
            var userId = int.Parse(User.FindFirst(AppClaims.Subject)!.Value, CultureInfo.InvariantCulture);
            var role = User.FindFirst(AppClaims.Role)!.Value;
            var granted = await permissions.GetPermissionsAsync(role, ct);

            return Ok(new CurrentUserDto(userId, role, granted));
        }

        // One generic answer for every failure: wrong password, unknown login, inactive user, bad token.
        private ObjectResult InvalidCredentials() =>
            Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials.");
    }
}
