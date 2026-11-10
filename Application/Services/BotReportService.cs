using Application.Common.Interfaces;
using Application.Dtos.Reports;
using Orders.Reports;

namespace Application.Services;

public sealed class BotReportService(ReportPdfService reports) : IBotReportService
{
    public async Task<ReportOutput> GenerateAsync(ReportRequest request, CancellationToken ct = default) =>
        (await reports.GenerateAsync(new GenerateReportPdf(Preset: ReportCatalog.Summary), request.RoleCode, ct))!;
}
