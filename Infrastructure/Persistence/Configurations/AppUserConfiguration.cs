using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    public class AppUserConfiguration : IEntityTypeConfiguration<AppUserEntity>
    {
        public void Configure(EntityTypeBuilder<AppUserEntity> builder)
        {
            builder.ToTable("app_users");
            builder.HasKey(u => u.Id);
            builder.Property(u => u.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            builder.Property(u => u.RoleId).HasColumnName("role_id").IsRequired();
            builder.Property(u => u.Login).HasColumnName("login").HasMaxLength(100).IsRequired();
            builder.Property(u => u.PasswordHash).HasColumnName("password_hash").IsRequired();
            builder.Property(u => u.FullName).HasColumnName("full_name").HasMaxLength(150).IsRequired();
            builder.Property(u => u.TelegramAllowed).HasColumnName("telegram_allowed").HasDefaultValue(false);
            builder.Property(u => u.TelegramUserId).HasColumnName("telegram_user_id");
            builder.Property(u => u.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            builder.Property(u => u.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            builder.Property(u => u.UpdatedAt).HasColumnName("updated_at").ValueGeneratedOnAddOrUpdate();
            builder.HasIndex(u => u.Login).IsUnique();
            builder.HasIndex(u => u.TelegramUserId).IsUnique();
            builder.HasIndex(u => u.RoleId);
            builder.HasIndex(u => u.TelegramAllowed);
        }
    }
}