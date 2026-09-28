using Application;
using Infrastructure;

namespace WebApi
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddAppServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddApplication()
                .AddInfrastructure(configuration);
            return services;
        }
    }
}
