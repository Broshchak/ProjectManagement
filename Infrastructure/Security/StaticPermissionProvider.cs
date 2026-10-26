using Application.Common.Interfaces;
using Application.Security;

namespace Infrastructure.Security
{
    public sealed class StaticPermissionProvider : IPermissionProvider
    {
        private static readonly Dictionary<string, HashSet<string>> Map = new(StringComparer.Ordinal)
        {
            [Roles.Admin] =
            [
                Permissions.OrdersRead, Permissions.OrdersCreate, Permissions.OrdersEdit,
                Permissions.UsersManage, Permissions.ReportsRead, Permissions.ReportsTelegramSend,
                Permissions.SystemDiagnostics, Permissions.TelegramLink
            ],
            [Roles.Manager] = [Permissions.OrdersRead, Permissions.OrdersCreate, Permissions.OrdersEdit],
            [Roles.Viewer] = [Permissions.OrdersRead],
            [Roles.Director] =
            [
                Permissions.OrdersRead, Permissions.ReportsRead, Permissions.ReportsTelegramSend,
                Permissions.TelegramLink
            ],
            [Roles.TelegramBot] = [Permissions.BotAct]
        };

        public Task<bool> HasPermissionAsync(string roleCode, string permission, CancellationToken ct = default) =>
            Task.FromResult(Map.TryGetValue(roleCode, out var set) && set.Contains(permission));

        public Task<IReadOnlyCollection<string>> GetPermissionsAsync(string roleCode, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<string>>(
                Map.TryGetValue(roleCode, out var set) ? set.Order(StringComparer.Ordinal).ToArray() : []);
    }
}
