namespace Domain.Entities
{
    public class RefreshTokenEntity
    {
        public long Id { get; set; }
        public int UserId { get; set; }
        public Guid FamilyId { get; set; }
        public string TokenHash { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime? RevokedAt { get; set; }

        public AppUserEntity User { get; set; } = null!;
    }
}
