using Application.Dtos.Auth;
using Domain.Entities;

namespace Application.Common.Interfaces
{
    public interface IUserRepository
    {
        Task<AppUserEntity?> GetByLoginAsync(string normalizedLogin, CancellationToken ct = default);
        Task<bool> LoginExistsAsync(string normalizedLogin, CancellationToken ct = default);
        Task<RoleEntity?> GetRoleByCodeAsync(string roleCode, CancellationToken ct = default);
        Task AddAsync(AppUserEntity user, CancellationToken ct = default);
        Task<AuthUserInfo?> GetActiveAuthInfoAsync(int userId, CancellationToken ct = default);
    }
}
