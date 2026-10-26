namespace Application.Common.Interfaces
{
    public interface IPermissionProvider
    {
        Task<bool> HasPermissionAsync(string roleCode, string permission, CancellationToken ct = default);
        Task<IReadOnlyCollection<string>> GetPermissionsAsync(string roleCode, CancellationToken ct = default);
    }
}
