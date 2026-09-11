using System.Windows.Controls;

namespace WpfClient.ViewModels;

public sealed record NavigationItem(string Key, string Title, UserControl View, string[] AllowedRoles)
{
    public bool IsAllowedFor(string roleName)
    {
        return AllowedRoles.Contains(roleName);
    }

    public string Icon => Key switch
    {
        "Dashboard" => "⌂",
        "Orders" => "□",
        "Products" => "◇",
        "Users" => "◉",
        "Reports" => "≡",
        _ => "•"
    };
}
