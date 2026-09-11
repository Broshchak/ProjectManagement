namespace Domain.Entities
{
    public class OrderStatusHistoryEntity
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int? PreviousStatusId { get; set; }
        public int NewStatusId { get; set; }
        public int? ChangedByUserId { get; set; }
        public DateTime ChangedAt { get; set; }
        public string? Comment { get; set; }
    }
}
