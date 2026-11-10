using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public sealed class ReportTemplateRepository(AppDbContext db) : IReportTemplateRepository
{
    public async Task<IReadOnlyList<ReportTemplateEntity>> ListAsync(CancellationToken ct) =>
        await db.Set<ReportTemplateEntity>().AsNoTracking().Where(t => t.IsActive).OrderBy(t => t.Name).ThenBy(t => t.Id).ToListAsync(ct);

    public Task<ReportTemplateEntity?> FindAsync(int id, CancellationToken ct) =>
        db.Set<ReportTemplateEntity>().SingleOrDefaultAsync(t => t.Id == id && t.IsActive, ct);

    public void Add(ReportTemplateEntity template) => db.Set<ReportTemplateEntity>().Add(template);
}
