using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Dtos.Reports;
using Orders.Reports;

namespace Application.Services;

public sealed class ReportPdfService(ReportTemplateService templates, IReportQueries queries, IReportPdfRenderer renderer)
{
    private const int RowLimit = 1000;

    public Task<FileReport?> GenerateAsync(GenerateReportPdf request, string role, CancellationToken ct) =>
        GenerateAsync(request, role, preview: false, ct);

    public Task<FileReport?> PreviewAsync(GenerateReportPdf request, string role, CancellationToken ct) =>
        GenerateAsync(request, role, preview: true, ct);

    private async Task<FileReport?> GenerateAsync(GenerateReportPdf request, string role, bool preview, CancellationToken ct)
    {
        if (request.Language is not ("uk" or "en") ||
            (request.Preset is not null ? 1 : 0) + (request.TemplateId.HasValue ? 1 : 0) + (request.Draft is not null ? 1 : 0) != 1)
            throw new ValidationException("Choose one preset, saved template or draft and a supported language.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try { return await GenerateAsync(request, role, preview, timeout.Token, DateTime.UtcNow); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && timeout.IsCancellationRequested)
        {
            throw new ServiceBusyException("Report generation exceeded its time limit. Try a shorter period.");
        }
    }

    private async Task<FileReport?> GenerateAsync(GenerateReportPdf request, string role, bool preview, CancellationToken ct, DateTime now)
    {
        var sources = await templates.GetSourcesAsync(role, ct);
        ReportLayout layout;
        string title;
        if (request.TemplateId is int id)
        {
            var template = await templates.GetAsync(id, role, ct);
            if (template is null) return null;
            if (request.ExpectedVersion != template.Version)
                throw new DataConflictException("The report template changed. Reload it before generating the PDF.");
            layout = template.Layout;
            title = template.Name;
        }
        else if (request.Draft is { } draft)
        {
            layout = draft;
            title = ReportCatalog.ValidateName(request.Title);
        }
        else
        {
            var preset = sources.FirstOrDefault(s => s.Label.Code == request.Preset) ?? throw new ReportAccessException();
            layout = preset.DefaultLayout;
            title = preset.Label.In(request.Language);
        }

        layout = ReportCatalog.Validate(layout, sources);
        var source = sources.Single(s => s.Label.Code == layout.Source);
        var generated = ReportCalendar.InKyiv(now);
        var today = DateOnly.FromDateTime(generated);
        ReportCatalog.ValidatePeriod(layout.Source, request.From, request.To);
        var from = request.From ?? layout.DefaultFrom;
        var to = request.To ?? layout.DefaultTo;
        if (layout.Source != ReportCatalog.LowStock && from is null)
        {
            from = today.AddDays(-29);
            to = today;
        }

        var culture = CultureInfo.GetCultureInfo(request.Language == "en" ? "en-GB" : "uk-UA");
        var (rows, metrics) = await ReadAsync(layout, from, to, request.Language, culture, preview, ct);
        var headers = layout.ShowTable ? layout.Columns.Select(code => source.Fields.Single(f => f.Code == code).In(request.Language)).ToArray() : [];
        var groups = layout.ShowTable ? MakeGroups(rows, layout, culture) : [];
        string? context = layout.Source is ReportCatalog.Orders or ReportCatalog.Summary or ReportCatalog.Products
            ? L(request.Language, "Дані за позиціями замовлень. Суми не підтверджують оплату.", "Order line data. Amounts do not confirm payment.")
            : L(request.Language, "Поточні залишки активних товарів, до 5 одиниць включно.", "Current stock of active products, up to 5 units.");
        if (preview)
            context = L(request.Language, "ПОПЕРЕДНІЙ ПЕРЕГЛЯД • Тестові дані", "PREVIEW • Sample data");
        var pdf = await renderer.RenderAsync(new ReportDocument(title, request.Language, from, to, generated, layout.Landscape,
            layout.Note, context, layout.ShowSummary ? metrics : [], headers, groups), ct);
        var name = preview ? $"preview-{layout.Source}.pdf" : $"{layout.Source}-{(to ?? today):yyyy-MM-dd}.pdf";
        return new FileReport(name, "application/pdf", pdf);
    }

    private async Task<(IReadOnlyList<Dictionary<string, IComparable>> Rows, IReadOnlyList<ReportMetric> Metrics)> ReadAsync(
        ReportLayout layout, DateOnly? from, DateOnly? to, string language, CultureInfo culture, bool preview, CancellationToken ct)
    {
        if (layout.Source == ReportCatalog.LowStock)
        {
            IReadOnlyList<LowStockProductReportItemDto> products = preview
                ? [new(1, L(language, "Блокнот А5", "A5 notebook"), L(language, "Канцелярія", "Stationery"), 3, 85m),
                   new(2, L(language, "Настільна лампа", "Desk lamp"), L(language, "Освітлення", "Lighting"), 1, 1240m)]
                : await queries.GetLowStockAsync(5, ct, RowLimit + 1);
            CheckCount(products.Count);
            return (products.Select(p => Row(("product", p.ProductName), ("category", p.CategoryName),
                ("quantity", p.Quantity), ("price", p.Price))).ToArray(),
                [Metric("Товарів", "Products", products.Count), Metric("Залишок", "Units in stock", products.Sum(p => (long)p.Quantity))]);
        }

        var start = ReportCalendar.ToUtc(from!.Value);
        var end = ReportCalendar.ToUtc(to!.Value.AddDays(1));
        if (layout.Source == ReportCatalog.Products)
        {
            IReadOnlyList<ProductSalesReportItemDto> products = preview
                ? [new(1, L(language, "Блокнот А5", "A5 notebook"), L(language, "Канцелярія", "Stationery"), 6, 510m),
                   new(2, L(language, "Настільна лампа", "Desk lamp"), L(language, "Освітлення", "Lighting"), 2, 2480m),
                   new(3, L(language, "Папка для документів", "Document folder"), L(language, "Канцелярія", "Stationery"), 4, 380m)]
                : await queries.GetProductSalesAsync(start, end, ct, RowLimit + 1);
            CheckCount(products.Count);
            return (products.Select(p => Row(("product", p.ProductName), ("category", p.CategoryName),
                ("quantity", p.Quantity), ("amount", p.TotalAmount))).ToArray(),
                [Metric("Товарів", "Products", products.Count), Metric("Одиниць", "Units", products.Sum(p => (long)p.Quantity)),
                 Metric("Сума", "Amount", products.Sum(p => p.TotalAmount))]);
        }

        var orders = preview ? SampleOrders(layout, start, language)
            : await queries.GetOrdersAsync(start, end, ct, RowLimit + 1, layout.Status, layout.Category);
        CheckCount(orders.Count);
        var rows = layout.Source == ReportCatalog.Summary
            ? ReportCatalog.Categories.Select(category =>
            {
                var matching = orders.Where(o => o.StatusCategory == category).ToArray();
                return Row(("category", Category(category, language)), ("count", matching.Length), ("amount", matching.Sum(o => o.TotalAmount)));
            }).ToArray()
            : orders.Select(o => Row(("number", o.OrderNumber), ("date", ReportCalendar.InKyiv(o.CreatedAt)),
                ("status", Status(o.StatusCode, o.StatusName, language)), ("status-code", o.StatusCode),
                ("category", Category(o.StatusCategory, language)), ("customer", o.CustomerFullName),
                ("amount", o.TotalAmount))).ToArray();

        return (rows, [Metric("Замовлень", "Orders", orders.Count), Metric("Сума", "Amount", orders.Sum(o => o.TotalAmount)),
            Metric("Середній чек", "Average order value", orders.Count == 0 ? 0m : orders.Average(o => o.TotalAmount))]);

        ReportMetric Metric(string uk, string en, IComparable value) => new(L(language, uk, en), Format(value, culture));
    }

    private static IReadOnlyList<OrderReportItemDto> SampleOrders(ReportLayout layout, DateTime start, string language)
    {
        (string Code, string Name, string Category)[] examples =
        [
            ("NewOrder", "New order", "New"), ("Registered", "Registered order", "InProgress"),
            ("Shipped", "Shipped order", "Shipped"), ("Invoiced", "Invoiced order", "Completed")
        ];
        return examples.Select((example, index) =>
        {
            string status = layout.Status ?? example.Code;
            var chosen = examples.FirstOrDefault(item => item.Code == status);
            string category = layout.Category ?? chosen.Category ?? example.Category;
            return new OrderReportItemDto(index + 1, $"TEST-{index + 1:D3}",
                $"{L(language, "Тестовий клієнт", "Sample customer")} {index + 1}",
                status, chosen.Name ?? status, (index + 1) * 245.50m, start.AddHours(9 + index), category);
        }).ToArray();
    }

    private static IReadOnlyList<ReportTableGroup> MakeGroups(IReadOnlyList<Dictionary<string, IComparable>> rows,
        ReportLayout layout, CultureInfo culture)
    {
        IEnumerable<Dictionary<string, IComparable>> sorted = rows;
        if (layout.SortBy is { } key)
            sorted = layout.Descending ? rows.OrderByDescending(row => row[key]) : rows.OrderBy(row => row[key]);
        var groupKey = layout.GroupBy == "status" ? "status-code" : layout.GroupBy;
        return sorted.GroupBy(row => groupKey is null ? null : row[groupKey])
            .Select(group => new ReportTableGroup(layout.GroupBy is { } key ? Format(group.First()[key], culture) : null,
                group.Select(row => (IReadOnlyList<string>)layout.Columns.Select(code => Format(row[code], culture)).ToArray()).ToArray()))
            .ToArray();
    }

    private static Dictionary<string, IComparable> Row(params (string Key, IComparable Value)[] cells) =>
        cells.ToDictionary(cell => cell.Key, cell => cell.Value);

    private static string Format(IComparable value, CultureInfo culture) => value switch
    {
        DateTime date => date.ToString("dd.MM.yyyy HH:mm", culture),
        decimal amount => amount.ToString("N2", culture),
        IFormattable formattable => formattable.ToString(null, culture),
        _ => value.ToString() ?? ""
    };

    private static string Category(string code, string lang) => code switch
    {
        "New" => L(lang, "Нові", "New"),
        "InProgress" => L(lang, "У роботі", "In progress"),
        "Shipped" => L(lang, "Відправлені", "Shipped"),
        "Completed" => L(lang, "Завершені", "Completed"),
        "Cancelled" => L(lang, "Скасовані", "Cancelled"),
        _ => code
    };

    internal static string Status(string code, string name, string language) => (code, name, language) switch
    {
        ("NewOrder", "New order", "uk") => "Нове",
        ("Registered", "Registered order", "uk") => "Зареєстроване",
        ("Granted", "Granted order", "uk") => "Погоджене",
        ("Shipped", "Shipped order", "uk") => "Відправлене",
        ("Invoiced", "Invoiced order", "uk") => "Рахунок виставлено",
        ("Cancelled", "Cancelled order", "uk") => "Скасоване",
        _ => name
    };

    private static string L(string language, string uk, string en) => language == "en" ? en : uk;

    private static void CheckCount(int count)
    {
        if (count > RowLimit)
            throw new ValidationException($"The report exceeds {RowLimit} rows. Narrow the period or filters.");
    }
}
