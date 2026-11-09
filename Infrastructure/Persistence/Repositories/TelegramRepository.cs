using Application.Common.Interfaces;
using Application.Dtos.Telegram;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.Persistence.Repositories
{
    public class TelegramRepository(AppDbContext context) : ITelegramRepository
    {
        public async Task AddLinkTokenAsync(TelegramLinkTokenEntity token, CancellationToken ct = default)
        {
            await context.Set<TelegramLinkTokenEntity>().AddAsync(token, ct);
        }

        public async Task<TelegramLinkTokenEntity?> GetLinkTokenAsync(string tokenHash, CancellationToken ct = default)
        {
            return await context.Set<TelegramLinkTokenEntity>()
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);
        }

        public async Task<bool> TryConsumeLinkTokenAsync(string tokenHash, DateTime now, CancellationToken ct = default)
        {
            var rows = await context.Set<TelegramLinkTokenEntity>()
                .Where(t => t.TokenHash == tokenHash && t.UsedAt == null && t.ExpiresAt > now)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, (DateTime?)now), ct);

            return rows == 1;
        }

        public async Task DeleteStaleLinkTokensAsync(int userId, DateTime now, CancellationToken ct = default)
        {
            await context.Set<TelegramLinkTokenEntity>()
                .Where(t => t.UserId == userId || t.ExpiresAt < now)
                .ExecuteDeleteAsync(ct);
        }

        public async Task<BotUserInfo?> GetBotUserAsync(long telegramUserId, CancellationToken ct = default)
        {
            return await (from u in context.Set<AppUserEntity>().AsNoTracking()
                          join r in context.Set<RoleEntity>().AsNoTracking() on u.RoleId equals r.Id
                          where u.TelegramUserId == telegramUserId && u.TelegramAllowed && u.IsActive
                          select new BotUserInfo(u.Id, u.FullName, r.Code)).FirstOrDefaultAsync(ct);
        }

        public async Task<bool> IsUserLinkedAsync(int userId, CancellationToken ct = default)
        {
            return await context.Set<AppUserEntity>()
                .AsNoTracking()
                .AnyAsync(u => u.Id == userId && u.TelegramUserId != null, ct);
        }

        public async Task<bool> IsTelegramAccountLinkedAsync(long telegramUserId, CancellationToken ct = default)
        {
            return await context.Set<AppUserEntity>()
                .AsNoTracking()
                .AnyAsync(u => u.TelegramUserId == telegramUserId, ct);
        }

        public async Task<bool> TryBindAsync(int userId, long telegramUserId, CancellationToken ct = default)
        {
            try
            {
                var rows = await context.Set<AppUserEntity>()
                    .Where(u => u.Id == userId && u.IsActive && u.TelegramUserId == null)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(u => u.TelegramUserId, (long?)telegramUserId)
                        .SetProperty(u => u.TelegramAllowed, true), ct);

                return rows == 1;
            }
            catch (Exception ex) when (IsUniqueViolation(ex))
            {
                return false; // this Telegram account was bound to someone else in the meantime
            }
        }

        public async Task<bool> UnbindAsync(int userId, CancellationToken ct = default)
        {
            var rows = await context.Set<AppUserEntity>()
                .Where(u => u.Id == userId && u.TelegramUserId != null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(u => u.TelegramUserId, (long?)null)
                    .SetProperty(u => u.TelegramAllowed, false), ct);

            return rows == 1;
        }

        public async Task<bool> UnbindByTelegramIdAsync(long telegramUserId, CancellationToken ct = default)
        {
            var rows = await context.Set<AppUserEntity>()
                .Where(u => u.TelegramUserId == telegramUserId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(u => u.TelegramUserId, (long?)null)
                    .SetProperty(u => u.TelegramAllowed, false), ct);

            return rows == 1;
        }

        private static bool IsUniqueViolation(Exception exception)
        {
            for (var e = exception; e is not null; e = e.InnerException)
            {
                if (e is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) return true;
            }
            return false;
        }
    }
}
