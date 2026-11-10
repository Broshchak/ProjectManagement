using Application.Dtos.Roles;

namespace Application.Common.Interfaces
{
    public interface IPermissionProvider
    {
        Task<AccessSnapshotDto> GetSnapshotAsync(string roleCode, CancellationToken ct = default);
        Task<bool> HasPermissionAsync(string roleCode, string permission, CancellationToken ct = default);
        Task<IReadOnlyCollection<string>> GetPermissionsAsync(string roleCode, CancellationToken ct = default);
    }
}
