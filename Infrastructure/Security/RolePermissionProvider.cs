using Application.Common.Interfaces;
using Application.Dtos.Roles;
using Application.Security;

namespace Infrastructure.Security;

public sealed class RolePermissionProvider(IRoleRepository roles) : IPermissionProvider
{
    public async Task<AccessSnapshotDto> GetSnapshotAsync(string roleCode, CancellationToken ct = default)
    {
        var visibility = await roles.GetActiveVisibilityAsync(roleCode, ct);
        if (visibility is null) return new(roleCode, 0, []);
        if (roleCode == Roles.TelegramBot) return new(roleCode, visibility.Revision, [Permissions.BotAct]);

        var known = VisibilityCatalog.Options.Select(p => p.Code);
        var granted = visibility.Permissions.Intersect(known, StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        if (!granted.Contains(Permissions.OrdersRead)) granted.Remove(Permissions.BoardRead);

        if (roleCode is Roles.Admin or Roles.Director or Roles.Manager && granted.Contains(Permissions.OrdersRead))
        {
            granted.Add(Permissions.OrdersEdit);
            if (granted.Contains(Permissions.CustomersRead)) granted.Add(Permissions.OrdersCreate);
        }
        if (roleCode is Roles.Admin or Roles.Director)
        {
            granted.Add(Permissions.TelegramLink);
            if (granted.Contains(Permissions.ProductsRead)) granted.Add(Permissions.ProductsManage);
            if (granted.Contains(Permissions.ReportsRead)) granted.Add(Permissions.ReportsTelegramSend);
        }
        if (roleCode == Roles.Admin)
        {
            granted.Add(Permissions.RolesManage);
            granted.Add(Permissions.RolesManageVisibility);
            granted.Add(Permissions.SystemDiagnostics);
            if (granted.Contains(Permissions.UsersRead)) granted.Add(Permissions.UsersManage);
        }
        if (roleCode is Roles.Admin or Roles.Director) granted.Add(Permissions.WorkflowManage);

        return new(roleCode, visibility.Revision, granted.Order(StringComparer.Ordinal).ToArray());
    }

    public async Task<bool> HasPermissionAsync(string roleCode, string permission, CancellationToken ct = default) =>
        (await GetSnapshotAsync(roleCode, ct)).Permissions.Contains(permission);

    public async Task<IReadOnlyCollection<string>> GetPermissionsAsync(string roleCode, CancellationToken ct = default) =>
        (await GetSnapshotAsync(roleCode, ct)).Permissions;
}
