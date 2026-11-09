using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class RefreshTokenRepository(AppDbContext context) : IRefreshTokenRepository
    {
        public async Task AddAsync(RefreshTokenEntity token, CancellationToken ct = default)
        {
            await context.Set<RefreshTokenEntity>().AddAsync(token, ct);
        }

        public async Task<RefreshTokenEntity?> GetByHashAsync(string tokenHash, CancellationToken ct = default)
        {
            return await context.Set<RefreshTokenEntity>()
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);
        }

        public async Task<bool> TryRevokeActiveAsync(string tokenHash, DateTime now, CancellationToken ct = default)
        {
            var rows = await context.Set<RefreshTokenEntity>()
                .Where(t => t.TokenHash == tokenHash && t.RevokedAt == null && t.ExpiresAt > now)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, (DateTime?)now), ct);

            return rows == 1;
        }

        public async Task RevokeFamilyAsync(Guid familyId, DateTime now, CancellationToken ct = default)
        {
            await context.Set<RefreshTokenEntity>()
                .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, (DateTime?)now), ct);
        }

        public async Task RevokeAllForUserAsync(int userId, DateTime now, CancellationToken ct = default)
        {
            await context.Set<RefreshTokenEntity>()
                .Where(t => t.UserId == userId && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, (DateTime?)now), ct);
        }

        public async Task DeleteExpiredAsync(int userId, DateTime now, CancellationToken ct = default)
        {
            await context.Set<RefreshTokenEntity>()
                .Where(t => t.UserId == userId && t.ExpiresAt < now)
                .ExecuteDeleteAsync(ct);
        }
    }
}
