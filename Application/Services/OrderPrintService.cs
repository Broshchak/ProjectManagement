using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Application.Common.Interfaces;
using Application.Dtos.Orders;
using Application.Dtos.Reports;

namespace Application.Services;

public sealed class OrderPrintService(IReportPdfRenderer renderer)
{
    public async Task<FileReport> GenerateAsync(OrderDetailDto order, string language, CancellationToken ct)
    {
        if (language is not ("uk" or "en")) throw new ValidationException("Choose a supported language.");
        var culture = CultureInfo.GetCultureInfo(language == "en" ? "en-GB" : "uk-UA");
        string T(string uk, string en) => language == "en" ? en : uk;
        string Amount(decimal value, string currency) => $"{value.ToString("N2", culture)} {currency}";
        string Date(DateTime value) => ReportCalendar.InKyiv(value).ToString("dd.MM.yyyy HH:mm", culture);

        List<string> details = [$"{T("Створено", "Created")}: {Date(order.CreatedAt)}",
            $"{T("Стан", "State")}: {ReportPdfService.Status(order.StatusCode, order.StatusName, language)}"];
        if (!string.IsNullOrWhiteSpace(order.CustomerFullName))
            details.Add($"{T("Клієнт", "Customer")}: {order.CustomerFullName}");
        if (!string.IsNullOrWhiteSpace(order.CustomerPhone))
            details.Add($"{T("Телефон", "Phone")}: {order.CustomerPhone}");
        if (!string.IsNullOrWhiteSpace(order.CustomerAddress))
            details.Add($"{T("Адреса", "Address")}: {order.CustomerAddress}");

        var rows = order.Items.Select(item => (IReadOnlyList<string>)[item.ProductName,
            item.Quantity.ToString(culture), Amount(item.UnitPrice, item.CurrencyCode), Amount(item.LineTotal, item.CurrencyCode)]).ToArray();
        var document = new ReportDocument($"{T("Замовлення", "Order")} {order.OrderNumber}", language,
            null, null, ReportCalendar.InKyiv(DateTime.UtcNow), false, order.Comment, string.Join(Environment.NewLine, details),
            [new(T("Позицій", "Line items"), rows.Length.ToString(culture)),
             new(T("Загальна сума", "Total amount"), Amount(order.TotalAmount, order.CurrencyCode))],
            [T("Товар", "Product"), T("Кількість", "Quantity"), T("Ціна", "Unit price"), T("Сума", "Amount")],
            rows.Length == 0 ? [] : [new(null, rows)]);
        return new($"order-{order.Id}.pdf", "application/pdf", await renderer.RenderAsync(document, ct));
    }
}
