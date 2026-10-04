using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    public class RoleConfiguration : IEntityTypeConfiguration<RoleEntity>
    {
        public void Configure(EntityTypeBuilder<RoleEntity> builder)
        {
            builder.ToTable("roles");
            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            builder.Property(r => r.Code).HasColumnName("code").HasMaxLength(30).IsRequired();
            builder.Property(r => r.Name).HasColumnName("name").HasMaxLength(80).IsRequired();

            builder.HasIndex(r => r.Code).IsUnique().HasDatabaseName("roles_code_key");
        }
    }
}
