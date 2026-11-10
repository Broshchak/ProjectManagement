using System.ComponentModel.DataAnnotations;
using Application.Common.Exceptions;
using Application.Security;
using Orders.Reports;

namespace Application.Services;

internal static class ReportCatalog
{
    public const string Orders = "orders";
    public const string Products = "products";
    public const string LowStock = "low-stock";
    public const string Summary = "summary";
    public static readonly string[] Categories = ["New", "InProgress", "Shipped", "Completed", "Cancelled"];

    private static readonly ReportLabel Amount = new("amount", "Сума", "Amount");
    private static readonly ReportLabel Product = new("product", "Товар", "Product");
    private static readonly ReportLabel Category = new("category", "Категорія", "Category");
    private static readonly ReportLabel Quantity = new("quantity", "Кількість", "Quantity");
    private static readonly ReportLabel Customer = new("customer", "Клієнт", "Customer");
    private static readonly ReportLabel Status = new("status", "Стан", "State");

    private static readonly ReportSource[] Sources =
    [
        new(new(Orders, "Замовлення", "Orders"),
            [new("number", "Номер", "Number"), new("date", "Дата", "Date"), Status, Category, Customer, Amount],
            ["status", "category"], new(Orders, ["number", "date", "status", "amount"], "date", Descending: true)),
        new(new(Products, "Товари в замовленнях", "Products in orders"),
            [Product, Category, Quantity, Amount], ["category"],
            new(Products, ["product", "category", "quantity", "amount"], "amount", Descending: true)),
        new(new(LowStock, "Низькі залишки", "Low stock"),
            [Product, Category, Quantity, new("price", "Ціна", "Price")], ["category"],
            new(LowStock, ["product", "category", "quantity", "price"], "quantity")),
        new(new(Summary, "Зведення замовлень", "Order summary"),
            [Category, new("count", "Замовлень", "Orders"), Amount], [],
            new(Summary, ["category", "count", "amount"]))
    ];

    public static IReadOnlyList<ReportSource> For(IReadOnlyCollection<string> grants)
    {
        if (!grants.Contains(Permissions.ReportsRead)) return [];
        return Sources.Where(s => s.Label.Code switch
            {
                Orders or Summary => grants.Contains(Permissions.OrdersRead),
                Products => grants.Contains(Permissions.ProductsRead) && grants.Contains(Permissions.OrdersRead),
                _ => grants.Contains(Permissions.ProductsRead)
            })
            .Select(s => s with { Fields = s.Fields.Where(f => f.Code != Customer.Code || grants.Contains(Permissions.CustomersRead)).ToArray() })
            .ToArray();
    }

    public static bool CanUse(ReportLayout layout, IReadOnlyList<ReportSource> allowed)
    {
        var source = allowed.FirstOrDefault(s => s.Label.Code == layout.Source);
        return source is not null && layout.Columns.All(code => source.Fields.Any(field => field.Code == code)) &&
            (layout.SortBy is null || source.Fields.Any(field => field.Code == layout.SortBy));
    }

    public static ReportLayout Validate(ReportLayout? layout, IReadOnlyList<ReportSource> allowed)
    {
        if (layout is null || layout.SchemaVersion != 1)
            throw new ValidationException("A report layout with schema version 1 is required.");
        var source = Sources.FirstOrDefault(s => s.Label.Code == layout.Source)
            ?? throw new ValidationException("Unknown report source.");

        var columns = layout.Columns;
        if (columns is null || columns.Count > source.Fields.Count || (layout.ShowTable && columns.Count == 0) ||
            columns.Distinct().Count() != columns.Count || columns.Any(code => !source.Fields.Any(field => field.Code == code)) ||
            (layout.SortBy is not null && !source.Fields.Any(field => field.Code == layout.SortBy)) ||
            (layout.GroupBy is not null && (!layout.ShowTable || !source.Groups.Contains(layout.GroupBy))))
            throw new ValidationException("Report columns, sorting or grouping are invalid for this source.");
        if (layout.Note?.Length > 1000 || (!layout.ShowTable && !layout.ShowSummary && string.IsNullOrWhiteSpace(layout.Note)))
            throw new ValidationException("Choose a report block and keep the note within 1000 characters.");
        if ((layout.Status is not null || layout.Category is not null) && layout.Source is not (Orders or Summary))
            throw new ValidationException("State filters require an order report.");
        if ((layout.Status is { } status && (status.Length is < 1 or > 30 ||
            !status.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))) ||
            (layout.Category is not null && !Categories.Contains(layout.Category)))
            throw new ValidationException("Invalid order state or category filter.");
        ValidatePeriod(layout.Source, layout.DefaultFrom, layout.DefaultTo);
        if (!CanUse(layout, allowed)) throw new ReportAccessException();
        return layout with { Note = string.IsNullOrWhiteSpace(layout.Note) ? null : layout.Note.Trim() };
    }

    public static void ValidatePeriod(string source, DateOnly? from, DateOnly? to)
    {
        if (from is null && to is null) return;
        if (source == LowStock || from is null || to is null ||
            from.Value.Year < 1970 || to.Value.Year > 9998 || to < from || to.Value.DayNumber - from.Value.DayNumber >= 366)
            throw new ValidationException("Select a valid period of at most 366 days. Low-stock reports use current values.");
    }

    public static string ValidateName(string? title) =>
        title?.Trim() is { Length: > 0 and <= 100 } name && !name.Any(char.IsControl)
            ? name : throw new ValidationException("A report name of up to 100 characters is required.");
}
