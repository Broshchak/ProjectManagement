using Application.Dtos.Auth;

namespace Application.Common.Interfaces;

public sealed record AccessToken(string Value, DateTime ExpiresAt);

public interface IAuthService
{
    /// <summary>Null = invalid credentials.</summary>
    Task<LoginResponseDto?> LoginAsync(LoginRequestDto request, CancellationToken ct = default);

    /// <summary>Null = user missing or inactive.</summary>
    Task<CurrentUserDto?> GetCurrentUserAsync(int userId, CancellationToken ct = default);
}

public interface IUserRepository
{
    Task<AuthUser?> GetByLoginAsync(string login, CancellationToken ct = default);
    Task<AuthUser?> GetByIdAsync(int id, CancellationToken ct = default);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface ITokenService
{
    AccessToken CreateAccessToken(AuthUser user);
}
