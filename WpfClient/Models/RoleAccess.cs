namespace WpfClient.Models;

public sealed record AccessSnapshot(string Role, long Revision, IReadOnlyList<string> Permissions);
public sealed record RoleInfo(int Id, string Code, string Name, bool IsActive, bool IsSystem, long Version);
public sealed record PermissionOption(string Code, string? Requires);
public sealed record RoleVisibility(int RoleId, long Revision, IReadOnlyList<string> Permissions);
