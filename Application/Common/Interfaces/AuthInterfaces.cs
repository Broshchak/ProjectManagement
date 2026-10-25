using Application.Dtos.Auth;
using Application.Dtos.Users;
using Domain.Entities;

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
    Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken ct = default);
    Task<UserDto?> GetDetailsAsync(int id, CancellationToken ct = default);
    Task<AppUserEntity?> FindAsync(int id, CancellationToken ct = default);
    Task<RoleEntity?> GetRoleAsync(string code, CancellationToken ct = default);
    Task<bool> IdentifiersInUseAsync(int? exceptUserId, string login, long? telegramUserId, CancellationToken ct = default);
    Task AddAsync(AppUserEntity user, CancellationToken ct = default);
    Task<IReadOnlyList<AppUserEntity>> GetWithPlaceholderPasswordsAsync(CancellationToken ct = default);
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
