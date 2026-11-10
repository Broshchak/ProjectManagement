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

            services.AddScoped<ITelegramLinkService, TelegramLinkService>();
            services.AddScoped<IBotService, BotService>();
            services.AddScoped<IBotReportService, BotReportService>();
            services.AddScoped<IProductService, ProductService>();
            services.AddScoped<ICustomerService, CustomerService>();
            services.AddScoped<IUserManagementService, UserManagementService>();
            services.AddScoped<RoleService>();
            services.AddScoped<IReportService, ReportService>();
            services.AddScoped<IOrderEditingService, OrderEditingService>();
            services.AddScoped<OrderWorkflowService>();

            return services;
        }
    }
}
