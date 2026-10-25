using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WpfClient.Models;

namespace WpfClient.Services;

public static class PdfReportExporter
{
    private static LocalizationManager T => LocalizationManager.Instance;
    public static void Save(
        string filePath,
        int selectedReportIndex,
        ReportSummary summary,
        DateTime reportDateFrom,
        DateTime reportDateTo,
        DateTime reportDate,
        int dateOrdersCount,
        decimal dateOrdersAmount,
        IEnumerable<TodayOrderReportItem> dateOrders,
        IEnumerable<ProductSalesReportItem> productSales,
        IEnumerable<LowStockProductReportItem> lowStockProducts,
        string managerText)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseSystemFonts = true;

        List<TodayOrderReportItem> dateOrdersList = dateOrders.ToList();
        List<ProductSalesReportItem> productSalesList = productSales.ToList();
        List<LowStockProductReportItem> lowStockList = lowStockProducts.ToList();

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(35);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(text => text.FontFamily("Arial").FontSize(10).FontColor("#17202B"));

                page.Header().Column(column =>
                {
                    column.Item().Text(GetTitle(selectedReportIndex)).FontSize(20).Bold().FontColor("#255C99");
                    column.Item().Text(T.Format("PdfGenerated", DateTime.Now)).FontSize(9).FontColor("#687789");
                    column.Item().PaddingTop(8).LineHorizontal(1).LineColor("#D9E1EA");
                });

                page.Content().PaddingVertical(14).Column(column =>
                {
                    column.Spacing(16);

                    if (selectedReportIndex == 1)
                    {
                        column.Item().Element(block => AddDateOrders(block, reportDate, dateOrdersCount, dateOrdersAmount, dateOrdersList));
                    }
                    else if (selectedReportIndex == 2)
                    {
                        column.Item().Element(block => AddProductSales(block, reportDateFrom, reportDateTo, productSalesList));
                    }
                    else if (selectedReportIndex == 3)
                    {
                        column.Item().Element(block => AddLowStock(block, lowStockList));
                    }
                    else
                    {
                        column.Item().Element(block => AddSummary(block, summary));
                        column.Item().Text(T.Format("PeriodRange", reportDateFrom, reportDateTo)).FontColor("#687789");
                        column.Item().Element(block => AddManagerText(block, managerText));
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span(T["PdfPage"]);
                    text.CurrentPageNumber();
                    text.Span(T["PdfOf"]);
                    text.TotalPages();
                });
            });
        }).GeneratePdf(filePath);
    }

    private static string GetTitle(int selectedReportIndex)
    {
        return selectedReportIndex switch
        {
            1 => T["ByDateTab"],
            2 => T["ProductsTab"],
            3 => T["LowStockTab"],
            _ => T["ReportSummaryTab"]
        };
    }

    private static void AddSummary(IContainer container, ReportSummary summary)
    {
        container.Column(column =>
        {
            column.Item().Text(T["ReportSummaryTab"]).FontSize(14).Bold().FontColor("#17202B");
            column.Item().PaddingTop(8).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                AddMetric(table, T["TotalOrders"], summary.TotalOrders.ToString());
                AddMetric(table, T["TotalAmount"], summary.TotalAmountText);
                AddMetric(table, T["AverageCheck"], summary.AverageOrderAmountText);
                AddMetric(table, T["LowStockTab"], summary.LowStockProducts.ToString());
            });

            column.Item().PaddingTop(8).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                AddSmallMetric(table, T["NewOrders"], summary.NewOrders);
                AddSmallMetric(table, T["InProgress"], summary.OrdersInProgress);
                AddSmallMetric(table, T["Shipped"], summary.ShippedOrders);
                AddSmallMetric(table, T["Invoiced"], summary.InvoicedOrders);
                AddSmallMetric(table, T["Cancelled"], summary.CancelledOrders);
            });

            column.Item().PaddingTop(8).Text(T.Format("PdfTopProduct", summary.TopProductText)).SemiBold();
        });
    }

    private static void AddManagerText(IContainer container, string managerText)
    {
        container.Column(column =>
        {
            column.Item().Text(T["ManagerReport"]).FontSize(14).Bold().FontColor("#17202B");
            column.Item()
                .PaddingTop(8)
                .Background("#F8FAFC")
                .Border(1)
                .BorderColor("#D9E1EA")
                .Padding(10)
                .Text(string.IsNullOrWhiteSpace(managerText) ? T["NoData"] : managerText)
                .LineHeight(1.25f);
        });
    }

    private static void AddDateOrders(
        IContainer container,
        DateTime reportDate,
        int ordersCount,
        decimal totalAmount,
        IReadOnlyList<TodayOrderReportItem> orders)
    {
        container.Column(column =>
        {
            column.Item().Text(T.Format("PdfDateOrders", reportDate)).FontSize(14).Bold().FontColor("#17202B");
            column.Item().PaddingTop(4).Text(T.Format("PdfDateSummary", ordersCount, totalAmount)).FontColor("#687789");

            column.Item().PaddingTop(8).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(70);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn();
                    columns.ConstantColumn(80);
                    columns.ConstantColumn(86);
                });

                AddHeader(table, T["Number"]);
                AddHeader(table, T["Customer"]);
                AddHeader(table, T["Status"]);
                AddHeader(table, T["Total"]);
                AddHeader(table, T["Created"]);

                foreach (TodayOrderReportItem order in orders)
                {
                    AddCell(table, order.OrderNumber);
                    AddCell(table, order.CustomerFullName);
                    AddCell(table, order.StatusText);
                    AddCell(table, order.TotalAmountText);
                    AddCell(table, order.CreatedAt.ToString("dd.MM.yyyy"));
                }

                if (orders.Count == 0)
                {
                    table.Cell().ColumnSpan(5).Element(CellStyle).Text(T["PdfNoOrders"]).FontColor("#687789");
                }
            });
        });
    }

    private static void AddProductSales(IContainer container, DateTime dateFrom, DateTime dateTo, IReadOnlyList<ProductSalesReportItem> products)
    {
        container.Column(column =>
        {
            column.Item().Text(T["ProductsTab"]).FontSize(14).Bold().FontColor("#17202B");
            column.Item().PaddingTop(4).Text(T.Format("PeriodRange", dateFrom, dateTo)).FontColor("#687789");
            column.Item().PaddingTop(8).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.RelativeColumn();
                    columns.ConstantColumn(70);
                    columns.ConstantColumn(95);
                });

                AddHeader(table, T["Product"]);
                AddHeader(table, T["Category"]);
                AddHeader(table, T["Quantity"]);
                AddHeader(table, T["Total"]);

                foreach (ProductSalesReportItem product in products)
                {
                    AddCell(table, product.ProductName);
                    AddCell(table, product.CategoryName);
                    AddCell(table, product.Quantity.ToString());
                    AddCell(table, product.TotalAmountText);
                }

                if (products.Count == 0)
                {
                    table.Cell().ColumnSpan(4).Element(CellStyle).Text(T["PdfNoProducts"]).FontColor("#687789");
                }
            });
        });
    }

    private static void AddLowStock(IContainer container, IReadOnlyList<LowStockProductReportItem> products)
    {
        container.Column(column =>
        {
            column.Item().Text(T["LowStockTab"]).FontSize(14).Bold().FontColor("#17202B");
            column.Item().PaddingTop(8).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.RelativeColumn();
                    columns.ConstantColumn(75);
                    columns.ConstantColumn(95);
                });

                AddHeader(table, T["Product"]);
                AddHeader(table, T["Category"]);
                AddHeader(table, T["Quantity"]);
                AddHeader(table, T["Price"]);

                foreach (LowStockProductReportItem product in products)
                {
                    AddCell(table, product.ProductName);
                    AddCell(table, product.CategoryName);
                    AddCell(table, product.Quantity.ToString());
                    AddCell(table, product.PriceText);
                }

                if (products.Count == 0)
                {
                    table.Cell().ColumnSpan(4).Element(CellStyle).Text(T["PdfNoLowStock"]).FontColor("#687789");
                }
            });
        });
    }

    private static void AddMetric(TableDescriptor table, string title, string value)
    {
        table.Cell().Element(MetricStyle).Column(column =>
        {
            column.Item().Text(title).FontSize(9).FontColor("#687789");
            column.Item().PaddingTop(3).Text(value).FontSize(14).Bold().FontColor("#17202B");
        });
    }

    private static void AddSmallMetric(TableDescriptor table, string title, int value)
    {
        table.Cell().Element(CellStyle).Column(column =>
        {
            column.Item().Text(title).FontSize(9).FontColor("#687789");
            column.Item().PaddingTop(3).Text(value.ToString()).FontSize(12).SemiBold();
        });
    }

    private static void AddHeader(TableDescriptor table, string text)
    {
        table.Cell().Element(HeaderStyle).Text(text).SemiBold();
    }

    private static void AddCell(TableDescriptor table, string text)
    {
        table.Cell().Element(CellStyle).Text(text);
    }

    private static IContainer HeaderStyle(IContainer container)
    {
        return container
            .Background("#F3F6FA")
            .Border(1)
            .BorderColor("#D9E1EA")
            .PaddingVertical(6)
            .PaddingHorizontal(7);
    }

    private static IContainer CellStyle(IContainer container)
    {
        return container
            .BorderBottom(1)
            .BorderColor("#EEF2F6")
            .PaddingVertical(6)
            .PaddingHorizontal(7);
    }

    private static IContainer MetricStyle(IContainer container)
    {
        return container
            .Background("#F8FAFC")
            .Border(1)
            .BorderColor("#D9E1EA")
            .Padding(10);
    }
}
