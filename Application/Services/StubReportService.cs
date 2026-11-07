using Application.Common.Interfaces;
using Application.Dtos.Reports;

namespace Application.Services
{
    /// <summary>Test stub: basic info only.</summary>
    public class StubReportService : IReportService
    {
        public Task<ReportOutput> GenerateAsync(ReportRequest request, CancellationToken ct = default)
        {
            var text =
                "Test report\n" +
                $"Channel: {request.Channel}\n" +
                $"Role: {request.RoleCode}\n" +
                $"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC\n\n" +
                "Real report generation is not implemented yet.";

            return Task.FromResult<ReportOutput>(new TextReport(text));
        }
    }
}
