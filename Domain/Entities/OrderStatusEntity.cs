namespace Domain.Entities
{
    public class OrderStatusEntity
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public short SortOrder { get; set; }   // SMALLINT in the schema
        public bool IsFinal { get; set; }
    }
}
