using System.ComponentModel.DataAnnotations;

namespace Application.Dtos.Auth;

public sealed record LoginRequestDto(
    [Required, StringLength(100)] string Login,
    [Required, StringLength(256)] string Password);

public sealed record LoginResponseDto(string AccessToken, DateTime AccessTokenExpiresAt, string TokenType = "Bearer");

public sealed record CurrentUserDto(int Id, string Login, string FullName, string Role);

/// <summary>Projection used by auth only (the original AppUserEntity has no Role navigation).</summary>
public sealed record AuthUser(int Id, string Login, string PasswordHash, string FullName, bool IsActive, string RoleCode);
