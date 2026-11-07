using Application.Common;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Dtos.Telegram;
using Domain.Entities;

namespace Application.Services
{
    public class TelegramLinkService(
        ITelegramRepository repository,
        ITokenService tokens,
        TelegramOptions options,
        IUnitOfWork unitOfWork) : ITelegramLinkService
    {
        public async Task<TelegramLinkDto> CreateLinkAsync(int userId, CancellationToken ct = default)
        {
            if (await repository.IsUserLinkedAsync(userId, ct))
            {
                throw new ConflictException("A Telegram account is already linked. Unlink it first.");
            }

            var now = DateTime.UtcNow;
            var expiresAt = now.AddMinutes(options.LinkTtlMinutes);
            var rawToken = tokens.GenerateOpaqueToken();

            await repository.DeleteStaleLinkTokensAsync(userId, now, ct); // only the newest link stays valid
            await repository.AddLinkTokenAsync(new TelegramLinkTokenEntity
            {
                UserId = userId,
                TokenHash = tokens.HashOpaqueToken(rawToken),
                ExpiresAt = expiresAt
            }, ct);
            await unitOfWork.SaveChangesAsync(ct);

            return new TelegramLinkDto($"https://t.me/{options.BotUsername}?start={rawToken}", expiresAt);
        }

        public async Task<TelegramStatusDto> GetStatusAsync(int userId, CancellationToken ct = default) =>
            new(await repository.IsUserLinkedAsync(userId, ct));

        public Task<bool> UnlinkAsync(int userId, CancellationToken ct = default) =>
            repository.UnbindAsync(userId, ct);
    }
}
