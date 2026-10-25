namespace WpfClient.Models;

public sealed class AuthenticatedUser
{
    public int Id { get; set; }
    public DateTimeOffset? AccessTokenExpiresAt { get; set; }
    public string Login { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;
}
