namespace Domain.Entities
{
    public class OrderEntity
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public int StatusId { get; set; }
        public int CustomerId { get; set; }
        public int? CreatedByUserId { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
