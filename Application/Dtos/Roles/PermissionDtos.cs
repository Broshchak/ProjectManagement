using System.ComponentModel.DataAnnotations;

namespace Application.Dtos.Roles;

public sealed record PermissionOptionDto(string Code, string? Requires = null);
public sealed record RoleVisibilityDto(int RoleId, long Revision, IReadOnlyList<string> Permissions);
public sealed record SetRoleVisibilityDto(long ExpectedRevision, [Required] IReadOnlyList<string> Permissions);
public sealed record AccessSnapshotDto(string Role, long Revision, IReadOnlyCollection<string> Permissions);
