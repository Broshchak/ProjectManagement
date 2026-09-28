using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    public class CustomerConfiguration : IEntityTypeConfiguration<CustomerEntity>
    {
        public void Configure(EntityTypeBuilder<CustomerEntity> builder)
        {
            builder.ToTable("customers");
            builder.HasKey(c => c.Id);
            builder.Property(c => c.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            builder.Property(c => c.FullName).HasColumnName("full_name").HasMaxLength(150).IsRequired();
            builder.Property(c => c.Phone).HasColumnName("phone").HasMaxLength(40);
            builder.Property(c => c.Email).HasColumnName("email").HasMaxLength(160);
            builder.Property(c => c.Address).HasColumnName("address");
            builder.Property(c => c.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            builder.Property(c => c.UpdatedAt).HasColumnName("updated_at").ValueGeneratedOnAddOrUpdate();
            builder.HasIndex(c => c.Email).IsUnique();
        }
    }
}
