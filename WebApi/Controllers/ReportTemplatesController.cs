using System.Globalization;
using Application.Security;
using Application.Services;
using Microsoft.AspNetCore.Mvc;
using Orders.Reports;
using WebApi.Security;

namespace WebApi.Controllers;

[ApiController]
[Route("api/report-templates")]
[HasPermission(Permissions.ReportsRead)]
public sealed class ReportTemplatesController(ReportTemplateService templates) : ControllerBase
{
    [HttpGet("catalog")]
    public async Task<IReadOnlyList<ReportSource>> GetCatalog(CancellationToken ct) => await templates.GetSourcesAsync(Role, ct);

    [HttpGet]
    public async Task<IReadOnlyList<ReportTemplate>> List(CancellationToken ct) => await templates.ListAsync(Role, ct);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ReportTemplate>> Get(int id, CancellationToken ct) =>
        await templates.GetAsync(id, Role, ct) is { } result ? Ok(result) : NotFound();

    [HttpPost]
    [HasPermission(Permissions.ReportTemplatesManage)]
    public async Task<ActionResult<ReportTemplate>> Create(SaveReportTemplate request, CancellationToken ct)
    {
        int userId = int.Parse(User.FindFirst(AppClaims.Subject)!.Value, CultureInfo.InvariantCulture);
        var saved = await templates.CreateAsync(request, userId, Role, ct);
        return CreatedAtAction(nameof(Get), new { id = saved.Id }, saved);
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.ReportTemplatesManage)]
    public async Task<ActionResult<ReportTemplate>> Update(int id, SaveReportTemplate request, CancellationToken ct) =>
        await templates.UpdateAsync(id, request, Role, ct) is { } saved ? Ok(saved) : NotFound();

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.ReportTemplatesManage)]
    public async Task<IActionResult> Delete(int id, [FromQuery] long expectedVersion, CancellationToken ct) =>
        await templates.DeleteAsync(id, expectedVersion, Role, ct) ? NoContent() : NotFound();

    private string Role => User.FindFirst(AppClaims.Role)!.Value;
}
