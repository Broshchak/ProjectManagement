namespace Domain.Entities;

public sealed class OrderWorkflowEntity
{
    public int Id { get; set; }
    public long Revision { get; set; }
    public long DraftVersion { get; set; }
    public string Published { get; set; } = "";
    public string Draft { get; set; } = "";
}
