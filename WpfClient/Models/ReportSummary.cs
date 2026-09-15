namespace WpfClient.Models;

public sealed class ReportSummary
{
    public int NewOrders { get; set; }

    public int OrdersInProgress { get; set; }

    public int ShippedOrders { get; set; }

    public int InvoicedOrders { get; set; }

    public decimal TotalAmount { get; set; }

    public string TotalAmountText => $"{TotalAmount:N2} грн";
}
