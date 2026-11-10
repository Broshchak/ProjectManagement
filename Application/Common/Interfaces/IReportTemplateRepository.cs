using Domain.Entities;

namespace Application.Common.Interfaces;

public interface IReportTemplateRepository
{
    Task<IReadOnlyList<ReportTemplateEntity>> ListAsync(CancellationToken ct);
    Task<ReportTemplateEntity?> FindAsync(int id, CancellationToken ct);
    void Add(ReportTemplateEntity template);
}
