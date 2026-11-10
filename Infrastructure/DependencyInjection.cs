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
            string provider = configuration["Database:Provider"] ?? "PostgreSQL";
            if (!provider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Database provider '{provider}' is not supported. Available provider: PostgreSQL.");
            string connectionString = configuration.GetConnectionString("Default")
                ?? throw new InvalidOperationException("ConnectionStrings:Default is required.");
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("ConnectionStrings:Default must not be empty.");
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(
                    connectionString,
                    npgsqlOptions => npgsqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)
                )
            );

            services.AddScoped<IOrderRepository, OrderRepository>();
            services.AddScoped<IOrderWorkflowRepository, OrderWorkflowRepository>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IRoleRepository, RoleRepository>();
            services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
            services.AddScoped<ITelegramRepository, TelegramRepository>();
            services.AddScoped<IProductRepository, ProductRepository>();
            services.AddScoped<ICustomerRepository, CustomerRepository>();
            services.AddScoped<IReportQueries, ReportQueries>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            services.AddSingleton(JwtOptions.FromConfiguration(configuration));
            services.AddSingleton<ITokenService, TokenService>();
            var hashing = PasswordHashingOptionsFactory.FromConfiguration(configuration);
            services.AddSingleton(hashing);
            services.AddSingleton<IPasswordHashAlgorithm>(new Pbkdf2Algorithm("pbkdf2-sha256", HashAlgorithmName.SHA256, hashing.Pbkdf2Sha256Iterations, hashing));
            services.AddSingleton<IPasswordHashAlgorithm>(new Pbkdf2Algorithm("pbkdf2-sha512", HashAlgorithmName.SHA512, hashing.Pbkdf2Sha512Iterations, hashing));
            services.AddSingleton<IPasswordHashAlgorithm>(new Argon2Algorithm("argon2id", Argon2Algorithm.TypeArgon2id, hashing));
            services.AddSingleton<IPasswordHashAlgorithm>(new Argon2Algorithm("argon2i", Argon2Algorithm.TypeArgon2i, hashing));
            services.AddSingleton<IPasswordHasher, PasswordHasherService>();
            services.AddScoped<IPermissionProvider, RolePermissionProvider>();

            services.AddSingleton(TelegramOptionsFactory.FromConfiguration(configuration));

            return services;
        }
    }
}
