using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    public class TelegramLinkTokenConfiguration : IEntityTypeConfiguration<TelegramLinkTokenEntity>
    {
        public void Configure(EntityTypeBuilder<TelegramLinkTokenEntity> builder)
        {
            builder.ToTable("telegram_link_tokens");
            builder.HasKey(t => t.Id);
            builder.Property(t => t.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            builder.Property(t => t.UserId).HasColumnName("user_id").IsRequired();
            builder.Property(t => t.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();
            builder.Property(t => t.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            builder.Property(t => t.ExpiresAt).HasColumnName("expires_at").IsRequired();
            builder.Property(t => t.UsedAt).HasColumnName("used_at");

            builder.HasIndex(t => t.TokenHash).IsUnique();
            builder.HasIndex(t => t.UserId);

            builder.HasOne<AppUserEntity>()
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
