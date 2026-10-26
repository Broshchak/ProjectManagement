using Application.Common.Interfaces;
using Application.Dtos.Reports;
using Application.Dtos.Telegram;
using Application.Security;

namespace Application.Services
{
    public class BotService(
        ITelegramRepository repository,
        ITokenService tokens,
        IPermissionProvider permissions,
        IReportService reports) : IBotService
    {
        public async Task<BotUserDto?> ResolveAsync(long telegramUserId, CancellationToken ct = default)
        {
            if (telegramUserId <= 0) return null;

            var user = await repository.GetBotUserAsync(telegramUserId, ct);
            return user is null ? null : await ToDtoAsync(user, ct);
        }

        public async Task<LinkRedeemResult> RedeemLinkAsync(long telegramUserId, string token, CancellationToken ct = default)
        {
            var invalid = new LinkRedeemResult(LinkRedeemOutcome.InvalidOrExpired, null);

            // Telegram user ids are positive. Negative ids are groups/channels and must never be bound.
            if (telegramUserId <= 0) return invalid;

            if (token is null || token.Length is < 20 or > 64 || !token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
            {
                return invalid;
            }

            var hash = tokens.HashOpaqueToken(token);
            var now = DateTime.UtcNow;

            var record = await repository.GetLinkTokenAsync(hash, ct);
            if (record is null || record.UsedAt is not null || record.ExpiresAt <= now) return invalid;

            if (await repository.IsTelegramAccountLinkedAsync(telegramUserId, ct))
            {
                return new LinkRedeemResult(LinkRedeemOutcome.TelegramAccountAlreadyLinked, null);
            }

            if (!await repository.TryConsumeLinkTokenAsync(hash, now, ct)) return invalid;

            if (!await repository.TryBindAsync(record.UserId, telegramUserId, ct))
            {
                return new LinkRedeemResult(LinkRedeemOutcome.BindFailed, null);
            }

            var user = await repository.GetBotUserAsync(telegramUserId, ct);
            return user is null
                ? new LinkRedeemResult(LinkRedeemOutcome.BindFailed, null)
                : new LinkRedeemResult(LinkRedeemOutcome.Linked, await ToDtoAsync(user, ct));
        }

        public Task<bool> UnlinkAsync(long telegramUserId, CancellationToken ct = default) =>
            telegramUserId <= 0 ? Task.FromResult(false) : repository.UnbindByTelegramIdAsync(telegramUserId, ct);

        public async Task<BotReportResult> GetReportAsync(long telegramUserId, CancellationToken ct = default)
        {
            var (access, user) = await AuthorizeAsync(telegramUserId,
                [Permissions.ReportsRead, Permissions.ReportsTelegramSend], ct);

            if (access != BotAccess.Allowed || user is null) return new BotReportResult(access, null);

            var output = await reports.GenerateAsync(
                new ReportRequest(user.UserId, user.RoleCode, ReportChannel.Telegram), ct);

            return new BotReportResult(BotAccess.Allowed, output);
        }

        private async Task<(BotAccess Access, BotUserInfo? User)> AuthorizeAsync(
            long telegramUserId, string[] requiredPermissions, CancellationToken ct)
        {
            if (telegramUserId <= 0) return (BotAccess.NotLinked, null);

            var user = await repository.GetBotUserAsync(telegramUserId, ct);
            if (user is null) return (BotAccess.NotLinked, null);

            foreach (var permission in requiredPermissions)
            {
                if (!await permissions.HasPermissionAsync(user.RoleCode, permission, ct))
                {
                    return (BotAccess.Forbidden, null);
                }
            }

            return (BotAccess.Allowed, user);
        }

        private async Task<BotUserDto> ToDtoAsync(BotUserInfo user, CancellationToken ct) =>
            new(user.UserId, user.FullName, user.RoleCode, await permissions.GetPermissionsAsync(user.RoleCode, ct));
    }
}
