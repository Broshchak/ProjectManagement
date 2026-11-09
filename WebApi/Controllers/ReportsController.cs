using Application.Common.Interfaces;
using Application.Dtos.Reports;
using Microsoft.AspNetCore.Mvc;
using Application.Security;
using WebApi.Security;

namespace WebApi.Controllers;

[Route("api/reports")]
[ApiController]
[HasPermission(Permissions.ReportsRead)]
public class ReportsController(IReportService reports, IPermissionProvider permissions) : ControllerBase
{
    [HttpGet("orders")]
    public async Task<ActionResult<OrderReportDto>> GetOrdersReport([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null, CancellationToken ct = default) =>
        Ok(await reports.GetOrdersAsync(from, to, ct));

    [HttpGet("orders/today")]
    public async Task<ActionResult<TodayOrdersReportDto>> GetTodayOrdersReport([FromQuery] DateTime? date = null, CancellationToken ct = default)
    {
        var report = await reports.GetTodayOrdersAsync(date, ct);
        if (await permissions.HasPermissionAsync(User.FindFirst(AppClaims.Role)!.Value, Permissions.CustomersRead, ct))
            return Ok(report);
        return Ok(report with { Orders = report.Orders.Select(o => o with { CustomerFullName = "" }).ToArray() });
    }

    [HttpGet("products")]
    public async Task<ActionResult<IReadOnlyCollection<ProductSalesReportItemDto>>> GetProductsReport([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null, CancellationToken ct = default) =>
        Ok(await reports.GetProductsAsync(from, to, ct));

    [HttpGet("low-stock")]
    public async Task<ActionResult<IReadOnlyCollection<LowStockProductReportItemDto>>> GetLowStockReport(CancellationToken ct = default) =>
        Ok(await reports.GetLowStockAsync(ct));

    [HttpGet("manager-summary")]
    public async Task<ActionResult<ManagerSummaryReportDto>> GetManagerSummaryReport([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null, CancellationToken ct = default) =>
        Ok(await reports.GetManagerSummaryAsync(from, to, ct));
}