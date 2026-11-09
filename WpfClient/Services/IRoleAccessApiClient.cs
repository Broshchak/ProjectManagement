using WpfClient.Models;

namespace WpfClient.Services;

public interface IRoleAccessApiClient
{
    Task<AccessSnapshot> GetMyAccessAsync(CancellationToken ct = default);
    Task<IReadOnlyList<RoleInfo>> GetRolesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PermissionOption>> GetPermissionCatalogAsync(CancellationToken ct = default);
    Task<IReadOnlyList<RoleVisibility>> GetRoleVisibilityAsync(CancellationToken ct = default);
    Task<RoleInfo> CreateRoleAsync(string code, string name, CancellationToken ct = default);
    Task<RoleInfo> RenameRoleAsync(int id, string name, long version, CancellationToken ct = default);
    Task<RoleInfo> SetRoleActiveAsync(int id, bool active, long version, CancellationToken ct = default);
    Task DeleteRoleAsync(int id, long version, CancellationToken ct = default);
    Task<RoleVisibility> SaveRoleVisibilityAsync(int id, long revision, IReadOnlyList<string> permissions, CancellationToken ct = default);
}
