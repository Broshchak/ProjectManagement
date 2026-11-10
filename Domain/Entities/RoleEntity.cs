namespace Domain.Entities
{
    public class RoleEntity
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public bool IsSystem { get; set; }
        public long Version { get; set; } = 1;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public ICollection<RolePermissionEntity> Visibility { get; set; } = new List<RolePermissionEntity>();
    }
}
