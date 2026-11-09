using System.Collections.Concurrent;
using Application.Common.Interfaces;
using Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace WebApi.Security
{
    public sealed class HasPermissionAttribute(string permission)
        : AuthorizeAttribute(PermissionPolicyProvider.Prefix + permission);

    public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
    {
        public string Permission { get; } = permission;
    }

    public sealed class PermissionHandler(IPermissionProvider permissions) : AuthorizationHandler<PermissionRequirement>
    {
        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
        {
            var role = context.User.FindFirst(AppClaims.Role)?.Value;
            if (role is null) return;

            if (await permissions.HasPermissionAsync(role, requirement.Permission))
            {
                context.Succeed(requirement);
            }
        }
    }

    /// <summary>Builds "perm:xxx" policies on demand, so no policy has to be registered per permission.</summary>
    public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : IAuthorizationPolicyProvider
    {
        public const string Prefix = "perm:";

        private readonly DefaultAuthorizationPolicyProvider _default = new(options);
        private readonly ConcurrentDictionary<string, AuthorizationPolicy> _cache = new();

        public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _default.GetDefaultPolicyAsync();

        public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _default.GetFallbackPolicyAsync();

        public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
        {
            if (!policyName.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return _default.GetPolicyAsync(policyName);
            }

            var policy = _cache.GetOrAdd(policyName, name =>
                new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .AddRequirements(new PermissionRequirement(name[Prefix.Length..]))
                    .Build());

            return Task.FromResult<AuthorizationPolicy?>(policy);
        }
    }
}
