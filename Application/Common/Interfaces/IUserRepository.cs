using Application.Dtos.Auth;
using Application.Dtos.Users;
using Domain.Entities;
using UserDetails = Application.Dtos.Users.UserDto;

namespace Application.Common.Interfaces;

public interface IUserRepository
{
    Task<AppUserEntity?> GetByLoginAsync(string normalizedLogin, CancellationToken ct = default);
    Task<RoleEntity?> GetRoleByCodeAsync(string roleCode, CancellationToken ct = default);
    Task AddAsync(AppUserEntity user, CancellationToken ct = default);
    Task<AuthUserInfo?> GetActiveAuthInfoAsync(int userId, CancellationToken ct = default);
    Task<IReadOnlyList<UserDetails>> GetAllAsync(CancellationToken ct = default);
    Task<UserDetails?> GetDetailsAsync(int id, CancellationToken ct = default);
    Task<AppUserEntity?> FindAsync(int id, CancellationToken ct = default);
    Task<bool> IdentifiersInUseAsync(int? exceptUserId, string login, long? telegramUserId, CancellationToken ct = default);
    Task<IReadOnlyList<AppUserEntity>> GetWithPlaceholderPasswordsAsync(CancellationToken ct = default);
}
