using Application.Security;
using Microsoft.AspNetCore.Mvc;
using WebApi.Security;

namespace WebApi.Controllers
{
    [Route("api/diagnostics")]
    [ApiController]
    [HasPermission(Permissions.SystemDiagnostics)]
    public class DiagnosticsController : ControllerBase
    {
        private static readonly HashSet<string> HiddenHeaders = new(StringComparer.OrdinalIgnoreCase)
        {
            "Authorization", "Cookie", "Set-Cookie", "Proxy-Authorization"
        };

        [HttpGet("client-info")]
        public IActionResult ClientInfo()
        {
            var ip = HttpContext.Connection.RemoteIpAddress;
            if (ip is { IsIPv4MappedToIPv6: true })
            {
                ip = ip.MapToIPv4();
            }

            var headers = Request.Headers
                .Where(h => !HiddenHeaders.Contains(h.Key))
                .ToDictionary(h => h.Key, h => h.Value.ToString());

            return Ok(new
            {
                clientIp = ip?.ToString(),

                // Set by the forwarded-headers middleware ONLY when it consumed an X-Forwarded-For header.
                // Contains the address of the connection that delivered the request (the proxy).
                originalConnectionIp = Request.Headers["X-Original-For"].ToString(),

                scheme = Request.Scheme,
                host = Request.Host.Value,
                headers
            });
        }
    }
}