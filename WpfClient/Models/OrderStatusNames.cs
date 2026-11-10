namespace WpfClient.Models;

public static class OrderStatusNames
{
    public static string ToText(string? status, string? name = null)
    {
        if (!string.IsNullOrWhiteSpace(name) && name != (status switch
        {
            "NewOrder" => "New order",
            "Registered" => "Registered order",
            "Granted" => "Granted order",
            "Shipped" => "Shipped order",
            "Invoiced" => "Invoiced order",
            "Cancelled" => "Cancelled order",
            _ => null
        })) return name;

        return status switch
        {
            "NewOrder" => LocalizationManager.Instance["NewOrder"],
            "Registered" => LocalizationManager.Instance["Registered"],
            "Granted" => LocalizationManager.Instance["Granted"],
            "Shipped" => LocalizationManager.Instance["Shipped"],
            "Invoiced" => LocalizationManager.Instance["Invoiced"],
            "Cancelled" => LocalizationManager.Instance["Cancelled"],
            _ => status ?? string.Empty
        };
    }

    public static string Category(string status) => status switch
    {
        "NewOrder" => "New",
        "Shipped" => "Shipped",
        "Invoiced" => "Completed",
        "Cancelled" => "Cancelled",
        _ => "InProgress"
    };
}
