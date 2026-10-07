using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Security;

public sealed class JwtSettings
{
    public const string Section = "Jwt";

    [Required] public string Issuer { get; set; } = string.Empty;
    [Required] public string Audience { get; set; } = string.Empty;

    [Required, MinLength(32)] public string SigningKey { get; set; } = string.Empty;

    [Range(1, 1440)] public int ExpiresMinutes { get; set; } = 2;

    public SymmetricSecurityKey CreateKey() => new(Encoding.UTF8.GetBytes(SigningKey));
}
