using System.ComponentModel.DataAnnotations;

namespace Application.Dtos.Roles;

public sealed record RoleDto(int Id, string Code, string Name, bool IsActive, bool IsSystem, long Version,
    DateTime CreatedAt, DateTime UpdatedAt);
public sealed record CreateRoleDto([Required, StringLength(30)] string Code, [Required, StringLength(80)] string Name);
public sealed record RenameRoleDto([Required, StringLength(80)] string Name, long ExpectedVersion);
public sealed record SetRoleActiveDto([Required] bool? IsActive, long ExpectedVersion);
