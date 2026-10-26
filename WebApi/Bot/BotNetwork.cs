using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace WebApi.Bot
{
    public sealed class BotNetworkOptions
    {
        private readonly List<System.Net.IPNetwork> _networks = [];

        public bool IsEnabled => _networks.Count > 0;

        public static BotNetworkOptions FromConfiguration(IConfiguration configuration)
        {
            var options = new BotNetworkOptions();
            var raw = configuration["Bot:AllowedIps"] ?? string.Empty;

            foreach (var entry in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (entry.Contains('/') && System.Net.IPNetwork.TryParse(entry, out var network))
                {
                    options._networks.Add(network);
                }
                else if (!entry.Contains('/') && IPAddress.TryParse(entry, out var address))
                {
                    var prefix = address.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;
                    options._networks.Add(new System.Net.IPNetwork(address, prefix));
                }
                else
                {
                    throw new InvalidOperationException($"Bot:AllowedIps contains an invalid entry: '{entry}'.");
                }
            }

            return options;
        }

        public bool IsAllowed(IPAddress? address)
        {
            if (!IsEnabled) return true;
            if (address is null) return false;
            if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();

            return _networks.Any(n => n.Contains(address));
        }
    }

    public sealed class BotNetworkFilter(BotNetworkOptions options, ILogger<BotNetworkFilter> logger) : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var address = context.HttpContext.Connection.RemoteIpAddress;
            if (options.IsAllowed(address)) return;

            logger.LogWarning("Bot endpoint called from a non-allowlisted address {Address}", address);

            var problem = new ProblemDetails { Status = StatusCodes.Status403Forbidden, Title = "Forbidden." };
            problem.Extensions["code"] = "network_denied";
            context.Result = new ObjectResult(problem) { StatusCode = StatusCodes.Status403Forbidden };
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
        }
    }
}
