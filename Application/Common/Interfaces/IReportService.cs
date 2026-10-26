using Application.Dtos.Reports;

namespace Application.Common.Interfaces
{
    public interface IReportService
    {
        Task<ReportOutput> GenerateAsync(ReportRequest request, CancellationToken ct = default);
    }
}
