using Application.Common.Interfaces;
using Application.Dtos.Auth;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class UserRepository(AppDbContext context) : IUserRepository
    {
        public async Task<AppUserEntity?> GetByLoginAsync(string normalizedLogin, CancellationToken ct = default)
        {
            return await context.Set<AppUserEntity>()
                .FirstOrDefaultAsync(u => u.Login == normalizedLogin, ct);
        }

        public async Task<bool> LoginExistsAsync(string normalizedLogin, CancellationToken ct = default)
        {
            return await context.Set<AppUserEntity>()
                .AsNoTracking()
                .AnyAsync(u => u.Login == normalizedLogin, ct);
        }

        public async Task<RoleEntity?> GetRoleByCodeAsync(string roleCode, CancellationToken ct = default)
        {
            return await context.Set<RoleEntity>()
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Code == roleCode, ct);
        }

        public async Task AddAsync(AppUserEntity user, CancellationToken ct = default)
        {
            await context.Set<AppUserEntity>().AddAsync(user, ct);
        }

        public async Task<AuthUserInfo?> GetActiveAuthInfoAsync(int userId, CancellationToken ct = default)
        {
            return await (from u in context.Set<AppUserEntity>().AsNoTracking()
                          join r in context.Set<RoleEntity>().AsNoTracking() on u.RoleId equals r.Id
                          where u.Id == userId && u.IsActive
                          select new AuthUserInfo(u.Id, r.Code)).FirstOrDefaultAsync(ct);
        }
    }
}
