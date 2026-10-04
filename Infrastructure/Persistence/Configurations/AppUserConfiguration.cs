using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    public class AppUserConfiguration : IEntityTypeConfiguration<AppUserEntity>
    {
        public void Configure(EntityTypeBuilder<AppUserEntity> builder)
        {
            builder.ToTable("app_users", t => t.HasCheckConstraint(
                "ck_app_users_telegram_user_id_required",
                "telegram_allowed = FALSE OR telegram_user_id IS NOT NULL"));

            builder.HasKey(u => u.Id);
            builder.Property(u => u.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            builder.Property(u => u.RoleId).HasColumnName("role_id").IsRequired();
            builder.Property(u => u.Login).HasColumnName("login").HasMaxLength(100).IsRequired();
            builder.Property(u => u.PasswordHash).HasColumnName("password_hash")
                .HasColumnType("text").IsRequired();
            builder.Property(u => u.FullName).HasColumnName("full_name")
                .HasMaxLength(150).IsRequired();
            builder.Property(u => u.TelegramAllowed).HasColumnName("telegram_allowed")
                .IsRequired().HasDefaultValue(false);
            builder.Property(u => u.TelegramUserId).HasColumnName("telegram_user_id");
            builder.Property(u => u.IsActive).HasColumnName("is_active")
                .IsRequired().HasDefaultValue(true);
            builder.Property(u => u.CreatedAt).HasColumnName("created_at")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("NOW()").ValueGeneratedOnAdd();
            builder.Property(u => u.UpdatedAt).HasColumnName("updated_at")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("NOW()").ValueGeneratedOnAddOrUpdate();

            builder.HasOne(u => u.Role)
                .WithMany()
                .HasForeignKey(u => u.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(u => u.Login).IsUnique().HasDatabaseName("app_users_login_key");
            builder.HasIndex(u => u.TelegramUserId).IsUnique()
                .HasDatabaseName("app_users_telegram_user_id_key");
            builder.HasIndex(u => u.RoleId).HasDatabaseName("ix_app_users_role_id");
            builder.HasIndex(u => u.TelegramAllowed)
                .HasDatabaseName("ix_app_users_telegram_allowed");
        }
    }
}
