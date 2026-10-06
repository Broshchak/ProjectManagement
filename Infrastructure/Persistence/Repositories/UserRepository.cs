using Application.Common.Interfaces;
using Application.Dtos.Auth;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public sealed class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<AuthUser?> GetByLoginAsync(string login, CancellationToken ct = default) =>
        Project(db.Set<AppUserEntity>().Where(u => u.Login == login)).FirstOrDefaultAsync(ct);

    public Task<AuthUser?> GetByIdAsync(int id, CancellationToken ct = default) =>
        Project(db.Set<AppUserEntity>().Where(u => u.Id == id)).FirstOrDefaultAsync(ct);

    // Filter on the ENTITY first, then project. EF cannot translate a Where on members of a constructor-built record.
    private IQueryable<AuthUser> Project(IQueryable<AppUserEntity> users) =>
        from u in users.AsNoTracking()
        join r in db.Set<RoleEntity>().AsNoTracking() on u.RoleId equals r.Id
        select new AuthUser(u.Id, u.Login, u.PasswordHash, u.FullName, u.IsActive, r.Code);
}
