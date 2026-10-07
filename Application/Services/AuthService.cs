using Application.Common.Interfaces;
using Application.Dtos.Auth;

namespace Application.Services;

public sealed class AuthService(IUserRepository users, IPasswordHasher hasher, ITokenService tokens) : IAuthService
{
    public async Task<LoginResponseDto?> LoginAsync(LoginRequestDto request, CancellationToken ct = default)
    {
        var user = await users.GetByLoginAsync(request.Login.Trim(), ct);
        if (user is null || !user.IsActive || !hasher.Verify(request.Password, user.PasswordHash))
            return null;

        var token = tokens.CreateAccessToken(user);
        return new LoginResponseDto(token.Value, token.ExpiresAt);
    }

    public async Task<CurrentUserDto?> GetCurrentUserAsync(int userId, CancellationToken ct = default)
    {
        var user = await users.GetByIdAsync(userId, ct);
        return user is null || !user.IsActive
            ? null
            : new CurrentUserDto(user.Id, user.Login, user.FullName, user.RoleCode);
    }
}
