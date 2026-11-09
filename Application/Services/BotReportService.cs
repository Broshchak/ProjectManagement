using Application.Common.Interfaces;
using Application.Dtos.Reports;

namespace Application.Services;

public sealed class BotReportService(IReportService reports) : IBotReportService
{
    public async Task<ReportOutput> GenerateAsync(ReportRequest request, CancellationToken ct = default) =>
        new TextReport((await reports.GetManagerSummaryAsync(null, null, ct)).Text);
}
