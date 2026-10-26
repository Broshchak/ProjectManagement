using System.Security.Cryptography;
using Application.Common.Interfaces;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Infrastructure.Security;
using Infrastructure.Security.PasswordHashing;
using Infrastructure.Telegram;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(
                    configuration.GetConnectionString("Default"),
                    npgsqlOptions =>
                    {
                        npgsqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                        npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(2), errorCodesToAdd: null);
                    }
                )
            );

            services.AddScoped<IOrderRepository, OrderRepository>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
            services.AddScoped<ITelegramRepository, TelegramRepository>();
            services.AddScoped<IProductRepository, ProductRepository>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<OrderEditingService>();

            services.AddSingleton(JwtOptions.FromConfiguration(configuration));
            services.AddSingleton<ITokenService, TokenService>();
            var hashing = PasswordHashingOptionsFactory.FromConfiguration(configuration);
            services.AddSingleton(hashing);
            services.AddSingleton<IPasswordHashAlgorithm>(new Pbkdf2Algorithm("pbkdf2-sha256", HashAlgorithmName.SHA256, hashing.Pbkdf2Sha256Iterations, hashing));
            services.AddSingleton<IPasswordHashAlgorithm>(new Pbkdf2Algorithm("pbkdf2-sha512", HashAlgorithmName.SHA512, hashing.Pbkdf2Sha512Iterations, hashing));
            services.AddSingleton<IPasswordHashAlgorithm>(new Argon2Algorithm("argon2id", Argon2Algorithm.TypeArgon2id, hashing));
            services.AddSingleton<IPasswordHashAlgorithm>(new Argon2Algorithm("argon2i", Argon2Algorithm.TypeArgon2i, hashing));
            services.AddSingleton<IPasswordHasher, PasswordHasherService>();
            services.AddSingleton<IPermissionProvider, StaticPermissionProvider>();

            services.AddSingleton(TelegramOptionsFactory.FromConfiguration(configuration));

            return services;
        }
    }
}
