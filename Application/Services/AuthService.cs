using Application.Common;
using Application.Common.Interfaces;
using Application.Dtos.Auth;
using Domain.Entities;

namespace Application.Services
{
    public class AuthService(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IPasswordHasher hasher,
        ITokenService tokens,
        IUnitOfWork unitOfWork) : IAuthService
    {
        public const int MaxPasswordLength = 128;
        private const int MaxRefreshTokenLength = 200;

        public async Task<TokenPairDto?> LoginAsync(LoginDto dto, CancellationToken ct = default)
        {
            var login = LoginRules.Normalize(dto.Login);
            var password = dto.Password ?? string.Empty;

            if (login.Length == 0 || password.Length == 0 || password.Length > MaxPasswordLength)
            {
                return null;
            }

            var user = await users.GetByLoginAsync(login, ct);
            if (user is null)
            {
                await hasher.VerifyDummyAsync(password, ct);
                return null;
            }

            var result = await hasher.VerifyAsync(user.PasswordHash, password, ct);
            if (result == PasswordVerifyResult.Failed || !user.IsActive)
            {
                return null;
            }

            if (result == PasswordVerifyResult.SuccessRehashNeeded)
            {
                user.PasswordHash = await hasher.HashAsync(password, ct);
            }

            var now = DateTime.UtcNow;
            await refreshTokens.DeleteExpiredAsync(user.Id, now, ct);

            return await IssueAsync(user.Id, Guid.NewGuid(), now, ct);
        }

        public async Task<TokenPairDto?> RefreshAsync(string refreshToken, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(refreshToken) || refreshToken.Length > MaxRefreshTokenLength)
            {
                return null;
            }

            var hash = tokens.HashRefreshToken(refreshToken);
            var now = DateTime.UtcNow;

            var existing = await refreshTokens.GetByHashAsync(hash, ct);
            if (existing is null) return null;

            if (existing.RevokedAt is not null)
            {
                await refreshTokens.RevokeFamilyAsync(existing.FamilyId, now, ct);
                return null;
            }

            if (existing.ExpiresAt <= now) return null;

            var info = await users.GetActiveAuthInfoAsync(existing.UserId, ct);
            if (info is null)
            {
                await refreshTokens.RevokeFamilyAsync(existing.FamilyId, now, ct);
                return null;
            }

            if (!await refreshTokens.TryRevokeActiveAsync(hash, now, ct))
            {
                await refreshTokens.RevokeFamilyAsync(existing.FamilyId, now, ct);
                return null;
            }

            return await IssueAsync(existing.UserId, existing.FamilyId, now, ct);
        }

        public async Task LogoutAsync(string refreshToken, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(refreshToken) || refreshToken.Length > MaxRefreshTokenLength) return;

            var existing = await refreshTokens.GetByHashAsync(tokens.HashRefreshToken(refreshToken), ct);
            if (existing is not null)
            {
                await refreshTokens.RevokeFamilyAsync(existing.FamilyId, DateTime.UtcNow, ct);
            }
        }

        private async Task<TokenPairDto> IssueAsync(int userId, Guid familyId, DateTime now, CancellationToken ct)
        {
            var rawRefreshToken = tokens.GenerateRefreshToken();
            var refreshExpiresAt = now.Add(tokens.RefreshTokenLifetime);

            await refreshTokens.AddAsync(new RefreshTokenEntity
            {
                UserId = userId,
                FamilyId = familyId,
                TokenHash = tokens.HashRefreshToken(rawRefreshToken),
                ExpiresAt = refreshExpiresAt
            }, ct);

            await unitOfWork.SaveChangesAsync(ct);

            var access = tokens.CreateAccessToken(userId);
            return new TokenPairDto(access.Token, access.ExpiresAt, rawRefreshToken, refreshExpiresAt);
        }
    }
}
