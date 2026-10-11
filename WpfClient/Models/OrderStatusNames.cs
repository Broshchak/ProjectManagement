namespace WpfClient.Models;

public static class OrderStatusNames
{
    public const string All = "Усі";

    public static string ToText(string? status)
    {
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

    public static string? ToCode(string statusText)
    {
        if (statusText == LocalizationManager.Instance["All"] || statusText == All)
        {
            return null;
        }

        return statusText switch
        {
            "Нове" or "New" => "NewOrder",
            "Зареєстроване" or "Registered" => "Registered",
            "Погоджене" or "Approved" => "Granted",
            "Відправлене" or "Shipped" => "Shipped",
            "Оплачене" or "Invoiced" => "Invoiced",
            "Скасоване" or "Cancelled" => "Cancelled",
            _ => statusText
        };
    }
}
