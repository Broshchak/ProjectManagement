using Application.Common.Interfaces;
using Application.Dtos.Auth;
using Application.Dtos.Users;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public sealed class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<AuthUser?> GetByLoginAsync(string login, CancellationToken ct = default) =>
        Project(db.Set<AppUserEntity>().Where(u => u.Login == login)).FirstOrDefaultAsync(ct);

    public Task<AuthUser?> GetByIdAsync(int id, CancellationToken ct = default) =>
        Project(db.Set<AppUserEntity>().Where(u => u.Id == id)).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken ct = default) =>
        await Details(db.Set<AppUserEntity>().OrderBy(u => u.FullName)).ToListAsync(ct);

    public Task<UserDto?> GetDetailsAsync(int id, CancellationToken ct = default) =>
        Details(db.Set<AppUserEntity>().Where(u => u.Id == id)).FirstOrDefaultAsync(ct);

    public Task<AppUserEntity?> FindAsync(int id, CancellationToken ct = default) =>
        db.Set<AppUserEntity>().FindAsync([id], ct).AsTask();

    public Task<RoleEntity?> GetRoleAsync(string code, CancellationToken ct = default) =>
        db.Set<RoleEntity>().FirstOrDefaultAsync(r => r.Code == code, ct);

    public Task<bool> IdentifiersInUseAsync(int? exceptUserId, string login, long? telegramUserId, CancellationToken ct = default) =>
        db.Set<AppUserEntity>().AnyAsync(u => (!exceptUserId.HasValue || u.Id != exceptUserId.Value) &&
            (u.Login.ToLower() == login || (telegramUserId.HasValue && u.TelegramUserId == telegramUserId)), ct);

    public async Task AddAsync(AppUserEntity user, CancellationToken ct = default) =>
        await db.Set<AppUserEntity>().AddAsync(user, ct);

    public async Task<IReadOnlyList<AppUserEntity>> GetWithPlaceholderPasswordsAsync(CancellationToken ct = default) =>
        await db.Set<AppUserEntity>().Where(u => u.PasswordHash.StartsWith("CHANGE_ME")).ToListAsync(ct);

    private IQueryable<UserDto> Details(IQueryable<AppUserEntity> users) =>
        from user in users.AsNoTracking()
        join role in db.Set<RoleEntity>().AsNoTracking() on user.RoleId equals role.Id
        select new UserDto(user.Id, user.Login, user.FullName, role.Code, user.IsActive, user.TelegramAllowed, user.TelegramUserId);

    // Filter on the ENTITY first, then project. EF cannot translate a Where on members of a constructor-built record.
    private IQueryable<AuthUser> Project(IQueryable<AppUserEntity> users) =>
        from u in users.AsNoTracking()
        join r in db.Set<RoleEntity>().AsNoTracking() on u.RoleId equals r.Id
        select new AuthUser(u.Id, u.Login, u.PasswordHash, u.FullName, u.IsActive, r.Code);
}
