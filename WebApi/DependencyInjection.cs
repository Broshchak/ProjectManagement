using System.Security.Claims;
using System.Threading.RateLimiting;
using Application;
using Application.Common.Interfaces;
using Application.Security;
using Infrastructure;
using Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using WebApi.Security;

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

        public static IServiceCollection AddWebApiSecurity(this IServiceCollection services, IConfiguration configuration)
        {
            var jwt = JwtOptions.FromConfiguration(configuration);

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.MapInboundClaims = false;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = jwt.Issuer,
                        ValidateAudience = true,
                        ValidAudience = jwt.Audience,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = jwt.SigningKey,
                        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                        RequireSignedTokens = true,
                        RequireExpirationTime = true,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromSeconds(30)
                    };

                    options.Events = new JwtBearerEvents
                    {
                        OnTokenValidated = async context =>
                        {
                            var subject = context.Principal?.FindFirst(AppClaims.Subject)?.Value;
                            if (!int.TryParse(subject, out var userId))
                            {
                                context.Fail("Invalid subject.");
                                return;
                            }

                            var users = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
                            var info = await users.GetActiveAuthInfoAsync(userId, context.HttpContext.RequestAborted);
                            if (info is null)
                            {
                                context.Fail("User not found or inactive.");
                                return;
                            }

                            ((ClaimsIdentity)context.Principal!.Identity!).AddClaim(new Claim(AppClaims.Role, info.RoleCode));
                        }
                    };
                });

            services.AddAuthorization(options =>
            {
                options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            });
            services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
            services.AddScoped<IAuthorizationHandler, PermissionHandler>();

            // Behind the host's reverse proxy, RemoteIpAddress is the proxy unless forwarded headers are honored.
            // Without this, the login rate limiter would put every user into one shared bucket.
            // Clearing the trusted-proxy lists is only safe if the app is reachable exclusively through the proxy.
            // If the host documents its proxy IPs, add them to KnownProxies instead.
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
                options.ForwardLimit = 1;
            });

            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                options.AddPolicy("login", httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

                options.AddPolicy("refresh", httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            });

            return services;
        }
    }
}
