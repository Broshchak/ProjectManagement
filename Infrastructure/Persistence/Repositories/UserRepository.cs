using Application.Common.Interfaces;
using Application.Dtos.Auth;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using UserDetails = Application.Dtos.Users.UserDto;

namespace Infrastructure.Persistence.Repositories;

public sealed class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<AppUserEntity?> GetByLoginAsync(string normalizedLogin, CancellationToken ct = default) =>
        db.Set<AppUserEntity>().FirstOrDefaultAsync(u => u.Login == normalizedLogin, ct);

    public Task<RoleEntity?> GetRoleByCodeAsync(string roleCode, CancellationToken ct = default) =>
        db.Set<RoleEntity>().AsNoTracking().FirstOrDefaultAsync(r => r.Code == roleCode, ct);

    public async Task AddAsync(AppUserEntity user, CancellationToken ct = default) =>
        await db.Set<AppUserEntity>().AddAsync(user, ct);

    public Task<AuthUserInfo?> GetActiveAuthInfoAsync(int userId, CancellationToken ct = default) =>
        (from user in db.Set<AppUserEntity>().AsNoTracking()
         join role in db.Set<RoleEntity>().AsNoTracking() on user.RoleId equals role.Id
         where user.Id == userId && user.IsActive
         select new AuthUserInfo(user.Id, role.Code)).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<UserDetails>> GetAllAsync(CancellationToken ct = default) =>
        await Details(db.Set<AppUserEntity>().OrderBy(u => u.FullName)).ToListAsync(ct);

    public Task<UserDetails?> GetDetailsAsync(int id, CancellationToken ct = default) =>
        Details(db.Set<AppUserEntity>().Where(u => u.Id == id)).FirstOrDefaultAsync(ct);

    public Task<AppUserEntity?> FindAsync(int id, CancellationToken ct = default) =>
        db.Set<AppUserEntity>().FindAsync([id], ct).AsTask();

    public Task<bool> IdentifiersInUseAsync(int? exceptUserId, string login, long? telegramUserId, CancellationToken ct = default) =>
        db.Set<AppUserEntity>().AnyAsync(u => (!exceptUserId.HasValue || u.Id != exceptUserId.Value) &&
            (u.Login.ToLower() == login || (telegramUserId.HasValue && u.TelegramUserId == telegramUserId)), ct);

    public async Task<IReadOnlyList<AppUserEntity>> GetWithPlaceholderPasswordsAsync(CancellationToken ct = default) =>
        await db.Set<AppUserEntity>().Where(u => u.PasswordHash.StartsWith("CHANGE_ME")).ToListAsync(ct);

    private IQueryable<UserDetails> Details(IQueryable<AppUserEntity> users) =>
        from user in users.AsNoTracking()
        join role in db.Set<RoleEntity>().AsNoTracking() on user.RoleId equals role.Id
        select new UserDetails(user.Id, user.Login, user.FullName, role.Code, user.IsActive, user.TelegramAllowed, user.TelegramUserId);
}
