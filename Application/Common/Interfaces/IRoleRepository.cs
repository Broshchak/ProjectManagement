using Domain.Entities;
using Application.Dtos.Roles;

namespace Application.Common.Interfaces;

public interface IRoleRepository
{
    Task<IReadOnlyList<RoleEntity>> ListAsync(CancellationToken ct);
    Task<RoleEntity?> FindAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<RoleVisibilityDto>> ListVisibilityAsync(CancellationToken ct);
    Task<RoleVisibilityDto?> GetActiveVisibilityAsync(string code, CancellationToken ct);
    Task SetVisibilityAsync(RoleEntity role, IReadOnlySet<string> codes, CancellationToken ct);
    Task<bool> CodeExistsAsync(string code, CancellationToken ct);
    Task<bool> HasUsersAsync(int roleId, CancellationToken ct);
    Task AddAsync(RoleEntity role, CancellationToken ct);
    void Delete(RoleEntity role);
}
