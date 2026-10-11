using System.Windows.Controls;
using WpfClient.Models;

namespace WpfClient.ViewModels;

public sealed record NavigationItem(string Key, string TitleKey, UserControl View, string[] AllowedRoles)
{
    public string Title => LocalizationManager.Instance[TitleKey];

    public bool IsAllowedFor(string roleName)
    {
        return AllowedRoles.Contains(roleName);
    }

    public string Icon => Key switch
    {
        "Dashboard" => "⌂",
        "Orders" => "□",
        "Kanban" => "▦",
        "Products" => "◇",
        "Users" => "◉",
        "Reports" => "≡",
        _ => "•"
    };
}
