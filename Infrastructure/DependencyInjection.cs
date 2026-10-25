using Application.Common.Interfaces;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
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
            services.AddScoped<IProductRepository, ProductRepository>();
            services.AddScoped<ICustomerRepository, CustomerRepository>();
            services.AddScoped<IReportQueries, ReportQueries>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            return services;
        }
    }
}
