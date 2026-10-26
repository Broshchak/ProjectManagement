using Application.Common.Interfaces;
using Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddScoped<IOrderService, OrderService>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<ITelegramLinkService, TelegramLinkService>();
            services.AddScoped<IBotService, BotService>();
            services.AddScoped<IReportService, StubReportService>();
            services.AddScoped<IProductService, ProductService>();

            return services;
        }
    }
}
