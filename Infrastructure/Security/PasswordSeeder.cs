using Application.Common.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Security;

public static class PasswordSeeder
{
    /// <summary>Replaces CHANGE_ME_* placeholder hashes from 02_seed_data.sql using Seed:Passwords:{login}.</summary>
    public static async Task SeedUserPasswordsAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        var pending = await db.Set<AppUserEntity>().Where(u => u.PasswordHash.StartsWith("CHANGE_ME")).ToListAsync(ct);
        foreach (var user in pending)
        {
            var password = config[$"Seed:Passwords:{user.Login}"];
            if (!string.IsNullOrWhiteSpace(password))
                user.PasswordHash = hasher.Hash(password);
        }

        if (pending.Count > 0)
            await db.SaveChangesAsync(ct);
    }
}
