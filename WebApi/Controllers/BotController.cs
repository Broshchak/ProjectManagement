using Application.Common.Interfaces;
using Application.Dtos.Reports;
using Application.Dtos.Telegram;
using Application.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WebApi.Bot;
using WebApi.Security;

namespace WebApi.Controllers
{
    [Route("api/bot/users/{telegramUserId:long}")]
    [ApiController]
    [HasPermission(Permissions.BotAct)]
    [ServiceFilter(typeof(BotNetworkFilter))]
    [EnableRateLimiting("bot-user")]
    public class BotController(IBotService botService, ILogger<BotController> logger) : ControllerBase
    {
        /// <summary>Who is this Telegram account? 404 not_linked if nobody (or an inactive user) is linked.</summary>
        [HttpGet]
        public async Task<IActionResult> Resolve(long telegramUserId, CancellationToken ct)
        {
            var user = await botService.ResolveAsync(telegramUserId, ct);
            return user is null ? NotLinked() : Ok(user);
        }

        /// <summary>Redeems a one-time link token (the payload of /start) and binds this Telegram account.</summary>
        [HttpPost("link")]
        [EnableRateLimiting("bot-link")]
        public async Task<IActionResult> RedeemLink(long telegramUserId, [FromBody] RedeemLinkDto dto, CancellationToken ct)
        {
            var result = await botService.RedeemLinkAsync(telegramUserId, dto.Token, ct);
            logger.LogInformation("Link redemption for Telegram user {TelegramUserId}: {Outcome}", telegramUserId, result.Outcome);

            return result.Outcome switch
            {
                LinkRedeemOutcome.Linked => Ok(result.User),
                LinkRedeemOutcome.InvalidOrExpired => Fail(400, "link_invalid", "The link is invalid or has expired."),
                LinkRedeemOutcome.TelegramAccountAlreadyLinked => Fail(409, "telegram_already_linked", "This Telegram account is already linked."),
                _ => Fail(409, "bind_failed", "Linking failed. Generate a new link.")
            };
        }

        /// <summary>Unlinks this Telegram account (the /unlink command).</summary>
        [HttpDelete("link")]
        public async Task<IActionResult> Unlink(long telegramUserId, CancellationToken ct)
        {
            var removed = await botService.UnlinkAsync(telegramUserId, ct);
            logger.LogInformation("Unlink for Telegram user {TelegramUserId}: {Removed}", telegramUserId, removed);

            return removed ? NoContent() : NotLinked();
        }

        /// <summary>
        /// Generates the report for this account. 200 with JSON { "type": "text", "text": "..." } or, for file reports,
        /// the raw file with Content-Type and Content-Disposition.
        /// </summary>
        [HttpPost("report")]
        public async Task<IActionResult> Report(long telegramUserId, CancellationToken ct)
        {
            var result = await botService.GetReportAsync(telegramUserId, ct);

            if (result.Access == BotAccess.NotLinked) return NotLinked();
            if (result.Access == BotAccess.Forbidden) return Fail(403, "forbidden", "This account may not use this command.");

            logger.LogInformation("Report generated for Telegram user {TelegramUserId}", telegramUserId);

            if (result.Output is TextReport text) return Ok(new { type = "text", text = text.Text });
            if (result.Output is FileReport file) return File(file.Content, file.ContentType, file.FileName);

            return Fail(500, "report_failed", "The report could not be generated.");
        }

        private ObjectResult NotLinked() => Fail(404, "not_linked", "No active user is linked to this Telegram account.");

        private ObjectResult Fail(int status, string code, string title)
        {
            var problem = new ProblemDetails { Status = status, Title = title };
            problem.Extensions["code"] = code;
            return StatusCode(status, problem);
        }
    }
}
