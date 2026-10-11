using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WpfClient.Models;

namespace WpfClient.Services;

public static class PdfReportExporter
{
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
                    column.Item().Text($"Сформовано: {DateTime.Now:dd.MM.yyyy HH:mm}").FontSize(9).FontColor("#687789");
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
                        column.Item().Text($"Період: {reportDateFrom:dd.MM.yyyy} - {reportDateTo:dd.MM.yyyy}").FontColor("#687789");
                        column.Item().Element(block => AddManagerText(block, managerText));
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Сторінка ");
                    text.CurrentPageNumber();
                    text.Span(" з ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf(filePath);
    }

    private static string GetTitle(int selectedReportIndex)
    {
        return selectedReportIndex switch
        {
            1 => "Замовлення за дату",
            2 => "Продажі по товарах",
            3 => "Низькі залишки",
            _ => "Загальне зведення"
        };
    }

    private static void AddSummary(IContainer container, ReportSummary summary)
    {
        container.Column(column =>
        {
            column.Item().Text("Загальне зведення").FontSize(14).Bold().FontColor("#17202B");
            column.Item().PaddingTop(8).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                AddMetric(table, "Замовлень", summary.TotalOrders.ToString());
                AddMetric(table, "Загальна сума", summary.TotalAmountText);
                AddMetric(table, "Середній чек", summary.AverageOrderAmountText);
                AddMetric(table, "Низькі залишки", summary.LowStockProducts.ToString());
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

                AddSmallMetric(table, "Нові", summary.NewOrders);
                AddSmallMetric(table, "В роботі", summary.OrdersInProgress);
                AddSmallMetric(table, "Відвантажені", summary.ShippedOrders);
                AddSmallMetric(table, "Рахунки", summary.InvoicedOrders);
                AddSmallMetric(table, "Скасовані", summary.CancelledOrders);
            });

            column.Item().PaddingTop(8).Text($"Топ-товар: {summary.TopProductText}").SemiBold();
        });
    }

    private static void AddManagerText(IContainer container, string managerText)
    {
        container.Column(column =>
        {
            column.Item().Text("Звіт для керівника").FontSize(14).Bold().FontColor("#17202B");
            column.Item()
                .PaddingTop(8)
                .Background("#F8FAFC")
                .Border(1)
                .BorderColor("#D9E1EA")
                .Padding(10)
                .Text(string.IsNullOrWhiteSpace(managerText) ? "Немає даних." : managerText)
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
            column.Item().Text($"Замовлення за дату {reportDate:dd.MM.yyyy}").FontSize(14).Bold().FontColor("#17202B");
            column.Item().PaddingTop(4).Text($"Кількість: {ordersCount}. Сума: {totalAmount:N2} грн").FontColor("#687789");

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

                AddHeader(table, "Номер");
                AddHeader(table, "Клієнт");
                AddHeader(table, "Статус");
                AddHeader(table, "Сума");
                AddHeader(table, "Створено");

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
                    table.Cell().ColumnSpan(5).Element(CellStyle).Text("Замовлень за цю дату немає.").FontColor("#687789");
                }
            });
        });
    }

    private static void AddProductSales(IContainer container, DateTime dateFrom, DateTime dateTo, IReadOnlyList<ProductSalesReportItem> products)
    {
        container.Column(column =>
        {
            column.Item().Text("Продажі по товарах").FontSize(14).Bold().FontColor("#17202B");
            column.Item().PaddingTop(4).Text($"Період: {dateFrom:dd.MM.yyyy} - {dateTo:dd.MM.yyyy}").FontColor("#687789");
            column.Item().PaddingTop(8).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.RelativeColumn();
                    columns.ConstantColumn(70);
                    columns.ConstantColumn(95);
                });

                AddHeader(table, "Товар");
                AddHeader(table, "Категорія");
                AddHeader(table, "К-сть");
                AddHeader(table, "Сума");

                foreach (ProductSalesReportItem product in products)
                {
                    AddCell(table, product.ProductName);
                    AddCell(table, product.CategoryName);
                    AddCell(table, product.Quantity.ToString());
                    AddCell(table, product.TotalAmountText);
                }

                if (products.Count == 0)
                {
                    table.Cell().ColumnSpan(4).Element(CellStyle).Text("Даних по товарах немає.").FontColor("#687789");
                }
            });
        });
    }

    private static void AddLowStock(IContainer container, IReadOnlyList<LowStockProductReportItem> products)
    {
        container.Column(column =>
        {
            column.Item().Text("Низькі залишки").FontSize(14).Bold().FontColor("#17202B");
            column.Item().PaddingTop(8).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.RelativeColumn();
                    columns.ConstantColumn(75);
                    columns.ConstantColumn(95);
                });

                AddHeader(table, "Товар");
                AddHeader(table, "Категорія");
                AddHeader(table, "Залишок");
                AddHeader(table, "Ціна");

                foreach (LowStockProductReportItem product in products)
                {
                    AddCell(table, product.ProductName);
                    AddCell(table, product.CategoryName);
                    AddCell(table, product.Quantity.ToString());
                    AddCell(table, product.PriceText);
                }

                if (products.Count == 0)
                {
                    table.Cell().ColumnSpan(4).Element(CellStyle).Text("Товарів з низьким залишком немає.").FontColor("#687789");
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
