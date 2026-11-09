using Application.Dtos.Roles;

namespace Application.Security;

public static class VisibilityCatalog
{
    public static IReadOnlyList<PermissionOptionDto> Options { get; } = Array.AsReadOnly<PermissionOptionDto>(
    [
        new(Permissions.DashboardRead),
        new(Permissions.OrdersRead),
        new(Permissions.BoardRead, Permissions.OrdersRead),
        new(Permissions.ProductsRead),
        new(Permissions.CustomersRead),
        new(Permissions.UsersRead),
        new(Permissions.ReportsRead),
        new(Permissions.ShipmentsRead),
        new(Permissions.PaymentsRead)
    ]);
}
