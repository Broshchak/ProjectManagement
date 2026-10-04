namespace Domain.Entities
{
    public class AppUserEntity
    {
        public int Id { get; set; }
        public int RoleId { get; set; }
        public string Login { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public bool TelegramAllowed { get; set; }
        public long? TelegramUserId { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public RoleEntity Role { get; set; } = null!;
    }
}
