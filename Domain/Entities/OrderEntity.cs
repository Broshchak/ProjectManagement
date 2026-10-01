namespace Domain.Entities
{
    public class OrderEntity
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public int StatusId { get; set; }
        public int CustomerId { get; set; }
        public int CreatedByUserId { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public OrderStatusEntity Status { get; set; } = null!;
        public CustomerEntity Customer { get; set; } = null!;
        public AppUserEntity CreatedByUser { get; set; } = null!;
        public ICollection<OrderItemEntity> Items { get; set; } = new List<OrderItemEntity>();
        public ICollection<OrderStatusHistoryEntity> StatusHistory { get; set; } = new List<OrderStatusHistoryEntity>();
    }
}
