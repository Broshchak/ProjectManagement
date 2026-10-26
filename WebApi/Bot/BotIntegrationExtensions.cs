using System.Threading.RateLimiting;

namespace WebApi.Bot
{
    public static class BotIntegrationExtensions
    {
        /// <summary>Network allowlist + per-Telegram-user rate limits for the bot endpoints.</summary>
        public static IServiceCollection AddBotIntegration(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton(BotNetworkOptions.FromConfiguration(configuration));
            services.AddScoped<BotNetworkFilter>();

            // AddRateLimiter accumulates configuration, so this adds to the login/refresh policies already registered.
            services.AddRateLimiter(options =>
            {
                options.AddPolicy("bot-user", context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        PartitionKey(context),
                        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

                options.AddPolicy("bot-link", context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        PartitionKey(context),
                        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            });

            return services;
        }

        // One bucket per Telegram account.
        private static string PartitionKey(HttpContext context) =>
            context.Request.RouteValues["telegramUserId"]?.ToString()
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";
    }
}
