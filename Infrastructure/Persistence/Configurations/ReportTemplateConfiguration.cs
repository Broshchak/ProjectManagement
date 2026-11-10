using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class ReportTemplateConfiguration : IEntityTypeConfiguration<ReportTemplateEntity>
{
    public void Configure(EntityTypeBuilder<ReportTemplateEntity> builder)
    {
        builder.ToTable("report_templates");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        builder.Property(t => t.Name).HasColumnName("name").HasMaxLength(100);
        builder.Property(t => t.OwnerUserId).HasColumnName("owner_user_id");
        builder.Property(t => t.Version).HasColumnName("version").IsConcurrencyToken();
        builder.Property(t => t.IsActive).HasColumnName("is_active");
        builder.Property(t => t.Settings).HasColumnName("settings").HasColumnType("jsonb");
        builder.HasOne<AppUserEntity>().WithMany().HasForeignKey(t => t.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
