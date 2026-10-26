using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Security
{
    public sealed class JwtOptions
    {
        public string Issuer { get; }
        public string Audience { get; }
        public int AccessTokenMinutes { get; }
        public int RefreshTokenDays { get; }
        public SymmetricSecurityKey SigningKey { get; }

        private JwtOptions(string issuer, string audience, string secret, int accessMinutes, int refreshDays)
        {
            Issuer = issuer;
            Audience = audience;
            AccessTokenMinutes = accessMinutes;
            RefreshTokenDays = refreshDays;
            SigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        }

        public static JwtOptions FromConfiguration(IConfiguration configuration)
        {
            var secret = configuration["Jwt:SigningKey"];
            if (string.IsNullOrEmpty(secret) || Encoding.UTF8.GetByteCount(secret) < 32)
            {
                throw new InvalidOperationException(
                    "Jwt:SigningKey is missing or shorter than 32 bytes. Set it via the Jwt__SigningKey environment variable.");
            }

            return new JwtOptions(
                configuration["Jwt:Issuer"] ?? "orders-api",
                configuration["Jwt:Audience"] ?? "orders-clients",
                secret,
                int.TryParse(configuration["Jwt:AccessTokenMinutes"], out var a) && a > 0 ? a : 15,
                int.TryParse(configuration["Jwt:RefreshTokenDays"], out var d) && d > 0 ? d : 14);
        }
    }
}
