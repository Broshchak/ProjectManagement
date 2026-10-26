using Application.Dtos.Telegram;
using Domain.Entities;

namespace Application.Common.Interfaces
{
    public interface ITelegramRepository
    {
        Task AddLinkTokenAsync(TelegramLinkTokenEntity token, CancellationToken ct = default);
        Task<TelegramLinkTokenEntity?> GetLinkTokenAsync(string tokenHash, CancellationToken ct = default);
        Task<bool> TryConsumeLinkTokenAsync(string tokenHash, DateTime now, CancellationToken ct = default);
        Task DeleteStaleLinkTokensAsync(int userId, DateTime now, CancellationToken ct = default);

        /// <summary>Linked AND allowed AND active user for this Telegram account, or null.</summary>
        Task<BotUserInfo?> GetBotUserAsync(long telegramUserId, CancellationToken ct = default);
        Task<bool> IsUserLinkedAsync(int userId, CancellationToken ct = default);
        Task<bool> IsTelegramAccountLinkedAsync(long telegramUserId, CancellationToken ct = default);

        /// <summary>Sets telegram_user_id + telegram_allowed in one statement. False if impossible (already linked, inactive, id taken).</summary>
        Task<bool> TryBindAsync(int userId, long telegramUserId, CancellationToken ct = default);
        Task<bool> UnbindAsync(int userId, CancellationToken ct = default);
        Task<bool> UnbindByTelegramIdAsync(long telegramUserId, CancellationToken ct = default);
    }

    public interface ITelegramLinkService
    {
        Task<TelegramLinkDto> CreateLinkAsync(int userId, CancellationToken ct = default);
        Task<TelegramStatusDto> GetStatusAsync(int userId, CancellationToken ct = default);
        Task<bool> UnlinkAsync(int userId, CancellationToken ct = default);
    }

    public interface IBotService
    {
        Task<BotUserDto?> ResolveAsync(long telegramUserId, CancellationToken ct = default);
        Task<LinkRedeemResult> RedeemLinkAsync(long telegramUserId, string token, CancellationToken ct = default);
        Task<bool> UnlinkAsync(long telegramUserId, CancellationToken ct = default);
        Task<BotReportResult> GetReportAsync(long telegramUserId, CancellationToken ct = default);
    }
}
