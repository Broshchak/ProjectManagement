using Application.Common.Interfaces;
using Application.Dtos.Reports;
using Microsoft.AspNetCore.Mvc;
using Application.Security;
using Application.Services;
using Orders.Reports;
using WebApi.Security;

namespace WebApi.Controllers;

[Route("api/reports")]
[ApiController]
[HasPermission(Permissions.ReportsRead)]
public class ReportsController(IReportService reports, IPermissionProvider permissions, ReportPdfService pdf) : ControllerBase
{
    [HttpPost("pdf")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GeneratePdf(GenerateReportPdf request, CancellationToken ct) =>
        await pdf.GenerateAsync(request, User.FindFirst(AppClaims.Role)!.Value, ct) is { } result
            ? File(result.Content, result.ContentType, result.FileName) : NotFound();

    [HttpPost("pdf/preview")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> PreviewPdf(GenerateReportPdf request, CancellationToken ct) =>
        await pdf.PreviewAsync(request, User.FindFirst(AppClaims.Role)!.Value, ct) is { } result
            ? File(result.Content, result.ContentType, result.FileName) : NotFound();

    [HttpGet("orders")]
    public async Task<ActionResult<OrderReportDto>> GetOrdersReport([FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null, [FromQuery] bool kyiv = false, CancellationToken ct = default) =>
        Ok(await reports.GetOrdersAsync(from, to, ct, kyiv));

    [HttpGet("orders/today")]
    public async Task<ActionResult<TodayOrdersReportDto>> GetTodayOrdersReport([FromQuery] DateTime? date = null,
        [FromQuery] bool kyiv = false, CancellationToken ct = default)
    {
        var report = await reports.GetTodayOrdersAsync(date, ct, kyiv);
        if (await permissions.HasPermissionAsync(User.FindFirst(AppClaims.Role)!.Value, Permissions.CustomersRead, ct))
            return Ok(report);
        return Ok(report with { Orders = report.Orders.Select(o => o with { CustomerFullName = "" }).ToArray() });
    }

    [HttpGet("products")]
    public async Task<ActionResult<IReadOnlyCollection<ProductSalesReportItemDto>>> GetProductsReport([FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null, [FromQuery] bool kyiv = false, CancellationToken ct = default) =>
        Ok(await reports.GetProductsAsync(from, to, ct, kyiv));

    [HttpGet("low-stock")]
    public async Task<ActionResult<IReadOnlyCollection<LowStockProductReportItemDto>>> GetLowStockReport(CancellationToken ct = default) =>
        Ok(await reports.GetLowStockAsync(ct));

    [HttpGet("manager-summary")]
    public async Task<ActionResult<ManagerSummaryReportDto>> GetManagerSummaryReport([FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null, [FromQuery] bool kyiv = false, CancellationToken ct = default) =>
        Ok(await reports.GetManagerSummaryAsync(from, to, ct, kyiv));
}
