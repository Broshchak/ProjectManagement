namespace WpfClient.ViewModels;

public sealed record TestUser(string Login, string FullName, string RoleName, string Password)
{
    public string DisplayName => $"{FullName} ({RoleName})";
}
