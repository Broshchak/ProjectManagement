namespace Application.Common.Interfaces
{
    public record AccessToken(string Token, DateTime ExpiresAt);

    public interface ITokenService
    {
        TimeSpan RefreshTokenLifetime { get; }
        AccessToken CreateAccessToken(int userId);
        string GenerateRefreshToken();
        string HashRefreshToken(string refreshToken);
        string GenerateOpaqueToken();
        string HashOpaqueToken(string token);
    }
}
