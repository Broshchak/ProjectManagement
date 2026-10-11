namespace WpfClient.Models;

public sealed class TodayOrderReportItem
{
    public int Id { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public string CustomerFullName { get; set; } = string.Empty;

    public string StatusCode { get; set; } = string.Empty;

    public string StatusName { get; set; } = string.Empty;

    public decimal TotalAmount { get; set; }

    public DateTime CreatedAt { get; set; }

    public string StatusText => string.IsNullOrWhiteSpace(StatusName) ? StatusCode : StatusName;

    public string TotalAmountText => $"{TotalAmount:N2} грн";

    public string CreatedAtText => CreatedAt.ToString("dd.MM.yyyy HH:mm");
}

public sealed class ProductSalesReportItem
{
    public int ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string CategoryName { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal TotalAmount { get; set; }

    public string TotalAmountText => $"{TotalAmount:N2} грн";
}

public sealed class LowStockProductReportItem
{
    public int ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string CategoryName { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal Price { get; set; }

    public string PriceText => $"{Price:N2} грн";
}
