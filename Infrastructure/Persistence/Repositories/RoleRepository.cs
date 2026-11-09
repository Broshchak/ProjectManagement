using Application.Common.Interfaces;
using Application.Dtos.Roles;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public sealed class RoleRepository(AppDbContext db) : IRoleRepository
{
    public async Task<IReadOnlyList<RoleEntity>> ListAsync(CancellationToken ct) =>
        await db.Set<RoleEntity>().AsNoTracking().OrderBy(r => r.Code).ToListAsync(ct);

    public Task<RoleEntity?> FindAsync(int id, CancellationToken ct) =>
        db.Set<RoleEntity>().FirstOrDefaultAsync(r => r.Id == id, ct);

    private IQueryable<RoleVisibilityDto> ProjectVisibility(IQueryable<RoleEntity> query) =>
        query.Select(r => new RoleVisibilityDto(r.Id, r.Version,
            r.Visibility.OrderBy(p => p.PermissionCode).Select(p => p.PermissionCode).ToArray()));

    public async Task<IReadOnlyList<RoleVisibilityDto>> ListVisibilityAsync(CancellationToken ct) =>
        await ProjectVisibility(db.Set<RoleEntity>().AsNoTracking().OrderBy(r => r.Id)).ToListAsync(ct);

    public Task<RoleVisibilityDto?> GetActiveVisibilityAsync(string code, CancellationToken ct) =>
        ProjectVisibility(db.Set<RoleEntity>().AsNoTracking().Where(r => r.Code == code && r.IsActive))
            .SingleOrDefaultAsync(ct);

    public async Task SetVisibilityAsync(RoleEntity role, IReadOnlySet<string> codes, CancellationToken ct)
    {
        await db.Entry(role).Collection(r => r.Visibility).LoadAsync(ct);
        var existing = role.Visibility.Select(p => p.PermissionCode).ToHashSet(StringComparer.Ordinal);
        db.RemoveRange(role.Visibility.Where(p => !codes.Contains(p.PermissionCode)).ToArray());
        foreach (var code in codes.Except(existing, StringComparer.Ordinal))
            role.Visibility.Add(new RolePermissionEntity { RoleId = role.Id, PermissionCode = code });
    }

    public Task<bool> CodeExistsAsync(string code, CancellationToken ct)
    {
        string normalized = code.ToLowerInvariant();
        return db.Set<RoleEntity>().AnyAsync(r => r.Code.ToLower() == normalized, ct);
    }

    public Task<bool> HasUsersAsync(int roleId, CancellationToken ct) =>
        db.Set<AppUserEntity>().AnyAsync(u => u.RoleId == roleId, ct);

    public async Task AddAsync(RoleEntity role, CancellationToken ct) =>
        await db.Set<RoleEntity>().AddAsync(role, ct);

    public void Delete(RoleEntity role) => db.Remove(role);
}
