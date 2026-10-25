using Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Security;

public static class PasswordSeeder
{
    /// <summary>Replaces CHANGE_ME_* placeholder hashes from 02_seed_data.sql using Seed:Passwords:{login}.</summary>
    public static async Task SeedUserPasswordsAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        var pending = await users.GetWithPlaceholderPasswordsAsync(ct);
        foreach (var user in pending)
        {
            var password = config[$"Seed:Passwords:{user.Login}"];
            if (!string.IsNullOrWhiteSpace(password))
                user.PasswordHash = hasher.Hash(password);
        }

        if (pending.Count > 0)
            await unitOfWork.SaveChangesAsync(ct);
    }
}
