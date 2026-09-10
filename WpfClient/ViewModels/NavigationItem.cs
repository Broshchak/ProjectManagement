using System.Windows.Controls;

namespace WpfClient.ViewModels;

public sealed record NavigationItem(string Key, string Title, UserControl View)
{
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
