namespace Domain.Entities
{
    public class ProductEntity
    {
        public int Id { get; set; }
        public int CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public ProductCategoryEntity Category { get; set; } = null!;
        public ProductPriceEntity? Price { get; set; }
        public ProductStockEntity? Stock { get; set; }
    }
}
