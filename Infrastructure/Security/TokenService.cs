using System.Buffers.Text;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Application.Common.Interfaces;
using Application.Security;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Security
{
    public sealed class TokenService(JwtOptions options) : ITokenService
    {
        private static readonly JsonWebTokenHandler Handler = new();

        public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(options.RefreshTokenDays);

        public AccessToken CreateAccessToken(int userId)
        {
            var now = DateTime.UtcNow;
            var expires = now.AddMinutes(options.AccessTokenMinutes);
            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = options.Issuer,
                Audience = options.Audience,
                Subject = new ClaimsIdentity(
                [
                    new Claim(AppClaims.Subject, userId.ToString(CultureInfo.InvariantCulture)),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
                ]),
                NotBefore = now,
                IssuedAt = now,
                Expires = expires,
                SigningCredentials = new SigningCredentials(options.SigningKey, SecurityAlgorithms.HmacSha256)
            };

            return new AccessToken(Handler.CreateToken(descriptor), expires);
        }

        public string GenerateRefreshToken() => GenerateOpaqueToken();

        public string HashRefreshToken(string refreshToken) => HashOpaqueToken(refreshToken);

        public string GenerateOpaqueToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

        public string HashOpaqueToken(string token) =>
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
