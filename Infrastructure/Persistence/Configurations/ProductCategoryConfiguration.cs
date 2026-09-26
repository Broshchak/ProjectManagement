using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    public class ProductCategoryConfiguration : IEntityTypeConfiguration<ProductCategoryEntity>
    {
        public void Configure(EntityTypeBuilder<ProductCategoryEntity> builder)
        {
            builder.ToTable("product_categories");
            builder.HasKey(pc => pc.Id);
            builder.Property(pc => pc.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            builder.Property(pc => pc.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
            builder.Property(pc => pc.Description).HasColumnName("description");
            builder.Property(pc => pc.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            builder.Property(pc => pc.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            builder.Property(pc => pc.UpdatedAt).HasColumnName("updated_at").ValueGeneratedOnAddOrUpdate();
            builder.HasIndex(pc => pc.Name).IsUnique();
        }
    }
}
