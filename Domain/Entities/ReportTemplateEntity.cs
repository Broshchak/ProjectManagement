namespace Domain.Entities;

public sealed class ReportTemplateEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int OwnerUserId { get; set; }
    public long Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public string Settings { get; set; } = "";
}
