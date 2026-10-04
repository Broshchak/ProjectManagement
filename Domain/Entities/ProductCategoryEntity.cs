namespace Domain.Entities
{
    public class ProductCategoryEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public ICollection<ProductEntity> Products { get; set; } = new List<ProductEntity>();
    }
}
