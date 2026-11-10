using System.Text.Json;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Security;
using Domain.Entities;
using Orders.Reports;

namespace Application.Services;

public sealed class ReportTemplateService(IReportTemplateRepository templates, IPermissionProvider permissions, IUnitOfWork unitOfWork)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<ReportSource>> GetSourcesAsync(string role, CancellationToken ct) =>
        ReportCatalog.For(await permissions.GetPermissionsAsync(role, ct));

    public async Task<IReadOnlyList<ReportTemplate>> ListAsync(string role, CancellationToken ct)
    {
        var allowed = await GetSourcesAsync(role, ct);
        return (await templates.ListAsync(ct)).Select(Map).Where(t => ReportCatalog.CanUse(t.Layout, allowed)).ToArray();
    }

    public async Task<ReportTemplate?> GetAsync(int id, string role, CancellationToken ct)
    {
        if (await templates.FindAsync(id, ct) is not { } entity) return null;
        var template = Map(entity);
        var allowed = await GetSourcesAsync(role, ct);
        if (!ReportCatalog.CanUse(template.Layout, allowed)) throw new ReportAccessException();
        return template;
    }

    public async Task<ReportTemplate> CreateAsync(SaveReportTemplate request, int userId, string role, CancellationToken ct)
    {
        var allowed = await GetSourcesAsync(role, ct);
        var layout = ReportCatalog.Validate(request.Layout, allowed);
        var entity = new ReportTemplateEntity
        {
            Name = ReportCatalog.ValidateName(request.Name), OwnerUserId = userId, Settings = JsonSerializer.Serialize(layout, Json)
        };
        templates.Add(entity);
        await unitOfWork.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<ReportTemplate?> UpdateAsync(int id, SaveReportTemplate request, string role, CancellationToken ct)
    {
        var allowed = await GetSourcesAsync(role, ct);
        if (await templates.FindAsync(id, ct) is not { } entity) return null;
        CheckEditable(entity, request.ExpectedVersion, allowed);
        var layout = ReportCatalog.Validate(request.Layout, allowed);
        entity.Name = ReportCatalog.ValidateName(request.Name);
        entity.Settings = JsonSerializer.Serialize(layout, Json);
        entity.Version++;
        await unitOfWork.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<bool> DeleteAsync(int id, long version, string role, CancellationToken ct)
    {
        var allowed = await GetSourcesAsync(role, ct);
        if (await templates.FindAsync(id, ct) is not { } entity) return false;
        CheckEditable(entity, version, allowed);
        entity.IsActive = false;
        entity.Version++;
        await unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    private static void CheckEditable(ReportTemplateEntity entity, long? version, IReadOnlyList<ReportSource> allowed)
    {
        if (!ReportCatalog.CanUse(Map(entity).Layout, allowed)) throw new ReportAccessException();
        if (version != entity.Version) throw new DataConflictException("The report template changed. Reload it before continuing.");
    }

    private static ReportTemplate Map(ReportTemplateEntity entity) => new(entity.Id, entity.Name, entity.OwnerUserId,
        entity.Version, JsonSerializer.Deserialize<ReportLayout>(entity.Settings, Json)!);
}
