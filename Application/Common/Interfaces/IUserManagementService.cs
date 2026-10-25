using Application.Dtos.Users;

namespace Application.Common.Interfaces;

public interface IUserManagementService
{
    Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken ct = default);
    Task<UserDto?> GetAsync(int id, CancellationToken ct = default);
    Task<UserDto?> SaveAsync(int? id, SaveUserDto request, string? password, CancellationToken ct = default);
    Task<bool> ChangePasswordAsync(int id, string password, CancellationToken ct = default);
}
