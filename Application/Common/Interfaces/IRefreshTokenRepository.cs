using Domain.Entities;

namespace Application.Common.Interfaces
{
    public interface IRefreshTokenRepository
    {
        Task AddAsync(RefreshTokenEntity token, CancellationToken ct = default);
        Task<RefreshTokenEntity?> GetByHashAsync(string tokenHash, CancellationToken ct = default);
        Task<bool> TryRevokeActiveAsync(string tokenHash, DateTime now, CancellationToken ct = default);
        Task RevokeFamilyAsync(Guid familyId, DateTime now, CancellationToken ct = default);
        Task RevokeAllForUserAsync(int userId, DateTime now, CancellationToken ct = default);
        Task DeleteExpiredAsync(int userId, DateTime now, CancellationToken ct = default);
    }
}
