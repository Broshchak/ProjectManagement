using System.ComponentModel.DataAnnotations;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Dtos.Roles;
using Application.Security;
using Domain.Entities;

namespace Application.Services;

public sealed class RoleService(IRoleRepository roles, IUnitOfWork unitOfWork, OrderWorkflowService workflow)
{
    public async Task<IReadOnlyList<RoleDto>> ListAsync(CancellationToken ct) =>
        (await roles.ListAsync(ct)).Select(Map).ToArray();

    public Task<IReadOnlyList<RoleVisibilityDto>> ListVisibilityAsync(CancellationToken ct) => roles.ListVisibilityAsync(ct);

    public async Task<RoleVisibilityDto?> SaveVisibilityAsync(int id, SetRoleVisibilityDto request, CancellationToken ct)
    {
        var role = await roles.FindAsync(id, ct);
        if (role is null) return null;
        CheckVersion(role, request.ExpectedRevision);
        if (role.Code == Roles.TelegramBot)
            throw new ValidationException("The service role does not have configurable visibility.");

        var codes = request.Permissions.ToHashSet(StringComparer.Ordinal);
        var allowed = VisibilityCatalog.Options;
        if (codes.Count != request.Permissions.Count || codes.Any(code => !allowed.Any(p => p.Code == code)))
            throw new ValidationException("Only distinct section visibility permissions may be saved.");
        if (allowed.Any(p => p.Requires is not null && codes.Contains(p.Code) && !codes.Contains(p.Requires)))
            throw new ValidationException("Board visibility requires order visibility.");

        await roles.SetVisibilityAsync(role, codes, ct);
        AdvanceRevision(role);
        await unitOfWork.SaveChangesAsync(ct);
        return new(role.Id, role.Version, codes.Order(StringComparer.Ordinal).ToArray());
    }

    public async Task<RoleDto?> GetAsync(int id, CancellationToken ct) =>
        await roles.FindAsync(id, ct) is { } role ? Map(role) : null;

    public async Task<RoleDto> CreateAsync(CreateRoleDto request, CancellationToken ct)
    {
        string code = request.Code.Trim();
        if (code.Length is < 2 or > 30 || !char.IsAsciiLetter(code[0]) ||
            !code.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-'))
            throw new ValidationException("Role code must begin with a letter and use 2–30 letters, digits, underscores or hyphens.");
        if (Roles.IsSystemCode(code) || await roles.CodeExistsAsync(code, ct))
            throw new DataConflictException("This role code is already in use or reserved.");
        var role = new RoleEntity { Code = code, Name = CleanName(request.Name) };
        await roles.AddAsync(role, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Map(role);
    }

    public Task<RoleDto?> RenameAsync(int id, RenameRoleDto request, CancellationToken ct) =>
        UpdateAsync(id, request.ExpectedVersion, role => role.Name = CleanName(request.Name), ct);

    public Task<RoleDto?> SetActiveAsync(int id, SetRoleActiveDto request, CancellationToken ct) =>
        UpdateAsync(id, request.ExpectedVersion, role =>
        {
            bool active = request.IsActive ?? throw new ValidationException("isActive is required.");
            if (!active && role.Code == Roles.Admin)
                throw new DataConflictException("The Admin role cannot be blocked.");
            role.IsActive = active;
        }, ct);

    public Task<bool> DeleteAsync(int id, long version, CancellationToken ct) =>
        unitOfWork.InTransactionAsync(token => DeleteRoleAsync(id, version, token), ct);

    private async Task<bool> DeleteRoleAsync(int id, long version, CancellationToken ct)
    {
        var role = await roles.FindAsync(id, ct);
        if (role is null) return false;
        CheckVersion(role, version);
        if (role.IsSystem || Roles.IsSystemCode(role.Code))
            throw new DataConflictException("System roles cannot be deleted.");
        if (await roles.HasUsersAsync(id, ct))
            throw new DataConflictException("This role is assigned to users, including inactive users.");
        if (await workflow.ReferencesRoleAsync(role.Code, ct))
            throw new DataConflictException("This role is used by a workflow transition or draft.");
        roles.Delete(role);
        await unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    private async Task<RoleDto?> UpdateAsync(int id, long version, Action<RoleEntity> update, CancellationToken ct)
    {
        var role = await roles.FindAsync(id, ct);
        if (role is null) return null;
        CheckVersion(role, version);
        update(role);
        AdvanceRevision(role);
        await unitOfWork.SaveChangesAsync(ct);
        return Map(role);
    }

    private static void AdvanceRevision(RoleEntity role)
    {
        role.Version++;
        role.UpdatedAt = DateTime.UtcNow;
    }

    private static string CleanName(string name)
    {
        name = name.Trim();
        return name.Length is > 0 and <= 80 ? name : throw new ValidationException("Role name is required (max 80 characters).");
    }

    private static void CheckVersion(RoleEntity role, long version)
    {
        if (role.Version != version) throw new DataConflictException("The role has changed. Reload it before trying again.");
    }

    private static RoleDto Map(RoleEntity role) =>
        new(role.Id, role.Code, role.Name, role.IsActive, role.IsSystem, role.Version, role.CreatedAt, role.UpdatedAt);
}
