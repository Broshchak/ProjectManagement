using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermissionEntity>
{
    public void Configure(EntityTypeBuilder<RolePermissionEntity> builder)
    {
        builder.ToTable("role_permissions");
        builder.HasKey(p => new { p.RoleId, p.PermissionCode });
        builder.Property(p => p.RoleId).HasColumnName("role_id");
        builder.Property(p => p.PermissionCode).HasColumnName("permission_code").HasMaxLength(50);
        builder.HasOne<RoleEntity>().WithMany(r => r.Visibility).HasForeignKey(p => p.RoleId).OnDelete(DeleteBehavior.Cascade);
    }
}
