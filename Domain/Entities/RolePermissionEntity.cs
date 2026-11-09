namespace Domain.Entities;

public sealed class RolePermissionEntity
{
    public int RoleId { get; set; }
    public string PermissionCode { get; set; } = "";
}
