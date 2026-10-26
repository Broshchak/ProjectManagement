using Application.Dtos.Auth;

namespace Application.Common.Interfaces
{
    public interface IAuthService
    {
        Task<TokenPairDto?> LoginAsync(LoginDto dto, CancellationToken ct = default);
        Task<TokenPairDto?> RefreshAsync(string refreshToken, CancellationToken ct = default);
        Task LogoutAsync(string refreshToken, CancellationToken ct = default);
    }
}
