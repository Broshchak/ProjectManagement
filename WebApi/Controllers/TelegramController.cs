using System.Globalization;
using Application.Common;
using Application.Common.Interfaces;
using Application.Security;
using Microsoft.AspNetCore.Mvc;
using WebApi.Security;

namespace WebApi.Controllers
{
    /// <summary>Linking endpoints for logged-in users (WPF client, admins). The bot uses BotController instead.</summary>
    [Route("api/telegram")]
    [ApiController]
    public class TelegramController(ITelegramLinkService linkService, TelegramOptions options) : ControllerBase
    {
        /// <summary>Creates a one-time deep link. The link is a credential: show it only to the logged-in user.</summary>
        [HttpPost("link")]
        [HasPermission(Permissions.TelegramLink)]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> CreateLink(CancellationToken ct)
        {
            if (!options.IsConfigured)
            {
                return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Telegram linking is not configured.");
            }

            return Ok(await linkService.CreateLinkAsync(CurrentUserId(), ct));
        }

        [HttpGet("link")]
        public async Task<IActionResult> GetLinkStatus(CancellationToken ct) =>
            Ok(await linkService.GetStatusAsync(CurrentUserId(), ct));

        [HttpDelete("link")]
        public async Task<IActionResult> Unlink(CancellationToken ct)
        {
            await linkService.UnlinkAsync(CurrentUserId(), ct);
            return NoContent();
        }

        [HttpDelete("~/api/users/{id:int}/telegram")]
        [HasPermission(Permissions.UsersManage)]
        public async Task<IActionResult> UnlinkUser(int id, CancellationToken ct) =>
            await linkService.UnlinkAsync(id, ct) ? NoContent() : NotFound();

        private int CurrentUserId()
        {
            var value = User.FindFirst(AppClaims.Subject)?.Value;
            return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                ? id
                : throw new InvalidOperationException("Authenticated request without a valid subject claim.");
        }
    }
}
