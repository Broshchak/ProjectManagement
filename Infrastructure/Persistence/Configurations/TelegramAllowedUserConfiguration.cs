using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    public class TelegramAllowedUserConfiguration : IEntityTypeConfiguration<TelegramAllowedUserEntity>
    {
        public void Configure(EntityTypeBuilder<TelegramAllowedUserEntity> builder)
        {
            builder.ToTable("telegram_allowed_users");
            builder.HasKey(t => t.Id);
            builder.Property(t => t.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            builder.Property(t => t.TelegramUserId).HasColumnName("telegram_user_id").IsRequired();
            builder.Property(t => t.RoleId).HasColumnName("role_id").IsRequired();
            builder.Property(t => t.DisplayName).HasColumnName("display_name").HasMaxLength(150);
            builder.Property(t => t.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            builder.Property(t => t.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            builder.HasIndex(t => t.TelegramUserId).IsUnique();
            builder.HasIndex(t => t.RoleId);
        }
    }
}
