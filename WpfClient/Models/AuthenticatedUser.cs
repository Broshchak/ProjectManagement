namespace WpfClient.Models;

public sealed class AuthenticatedUser
{
    public string Login { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;
}
