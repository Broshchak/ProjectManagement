using System.Globalization;
using System.Security.Claims;
using Application.Common.Interfaces;
using Application.Dtos.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Security;

public sealed class JwtTokenService(IOptions<JwtSettings> options) : ITokenService
{
    private readonly JwtSettings _settings = options.Value;
    private readonly SigningCredentials _credentials = new(options.Value.CreateKey(), SecurityAlgorithms.HmacSha256);
    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken CreateAccessToken(AuthUser user)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_settings.ExpiresMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("sub", user.Id.ToString(CultureInfo.InvariantCulture)),
                new Claim("role", user.RoleCode)
            }),
            SigningCredentials = _credentials
        };

        return new AccessToken(_handler.CreateToken(descriptor), expires);
    }
}
