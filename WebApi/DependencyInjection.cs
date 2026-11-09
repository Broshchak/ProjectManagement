using System.Security.Claims;
using System.Net;
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
                options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser()
                    .RequireAssertion(context => context.User.FindFirst(AppClaims.Role)?.Value is { } role && role != Roles.TelegramBot)
                    .Build();
            });
            services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
            services.AddScoped<IAuthorizationHandler, PermissionHandler>();

            // The bot allowlist and rate limits use the client IP. Trust forwarding only from known proxies.
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.ForwardLimit = 1;
                foreach (var entry in (configuration["ReverseProxy:KnownProxies"] ?? string.Empty)
                             .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (!IPAddress.TryParse(entry, out var address))
                        throw new InvalidOperationException("ReverseProxy:KnownProxies must contain comma-separated IP addresses.");
                    options.KnownProxies.Add(address);
                }
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
