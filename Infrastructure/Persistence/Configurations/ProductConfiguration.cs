using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    public class ProductConfiguration : IEntityTypeConfiguration<ProductEntity>
    {
        public void Configure(EntityTypeBuilder<ProductEntity> builder)
        {
            builder.ToTable("products");
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            builder.Property(p => p.CategoryId).HasColumnName("category_id").IsRequired();
            builder.Property(p => p.Name).HasColumnName("name").HasMaxLength(160).IsRequired();
            builder.Property(p => p.Description).HasColumnName("description").HasColumnType("text");
            builder.Property(p => p.IsActive).HasColumnName("is_active")
                .IsRequired().HasDefaultValue(true);
            builder.Property(p => p.CreatedAt).HasColumnName("created_at")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("NOW()").ValueGeneratedOnAdd();
            builder.Property(p => p.UpdatedAt).HasColumnName("updated_at")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("NOW()").ValueGeneratedOnAddOrUpdate();

            builder.HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(p => new { p.CategoryId, p.Name }).IsUnique()
                .HasDatabaseName("uq_products_category_name");
            builder.HasIndex(p => p.CategoryId).HasDatabaseName("ix_products_category_id");
            builder.HasIndex(p => p.IsActive).HasDatabaseName("ix_products_is_active");
        }
    }
}
