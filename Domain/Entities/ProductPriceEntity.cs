namespace Domain.Entities
{
    public class ProductPriceEntity
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public decimal Price { get; set; }
        public string CurrencyCode { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
