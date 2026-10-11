namespace WpfClient.Models;

public sealed class ReportSummary
{
    public int TotalOrders { get; set; }

    public int NewOrders { get; set; }

    public int OrdersInProgress { get; set; }

    public int ShippedOrders { get; set; }

    public int InvoicedOrders { get; set; }

    public int CancelledOrders { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal AverageOrderAmount { get; set; }

    public int LowStockProducts { get; set; }

    public bool HasLowStockProducts => LowStockProducts > 0;

    public string TopProductName { get; set; } = "-";

    public int TopProductQuantity { get; set; }

    public string TotalAmountText => $"{TotalAmount:N2} грн";

    public string AverageOrderAmountText => $"{AverageOrderAmount:N2} грн";

    public string LowStockProductsText => LowStockProducts == 0
        ? string.Empty
        : LocalizationManager.Instance.Format("NeedsAttention", LowStockProducts);

    public string TopProductText => TopProductQuantity == 0
        ? LocalizationManager.Instance["NoData"]
        : $"{TopProductName} ({TopProductQuantity} од.)";
}
