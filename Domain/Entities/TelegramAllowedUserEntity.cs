namespace Domain.Entities
{
    public class TelegramAllowedUserEntity
    {
        public int Id { get; set; }
        public long TelegramUserId { get; set; }
        public int RoleId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
