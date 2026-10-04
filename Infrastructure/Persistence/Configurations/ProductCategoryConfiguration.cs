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
            builder.Property(pc => pc.Description).HasColumnName("description")
                .HasColumnType("text");
            builder.Property(pc => pc.IsActive).HasColumnName("is_active")
                .IsRequired().HasDefaultValue(true);
            builder.Property(pc => pc.CreatedAt).HasColumnName("created_at")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("NOW()").ValueGeneratedOnAdd();
            builder.Property(pc => pc.UpdatedAt).HasColumnName("updated_at")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("NOW()").ValueGeneratedOnAddOrUpdate();

            builder.HasIndex(pc => pc.Name).IsUnique()
                .HasDatabaseName("product_categories_name_key");
        }
    }
}
