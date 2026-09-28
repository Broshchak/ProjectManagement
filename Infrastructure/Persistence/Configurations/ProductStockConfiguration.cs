using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    public class ProductStockConfiguration : IEntityTypeConfiguration<ProductStockEntity>
    {
        public void Configure(EntityTypeBuilder<ProductStockEntity> builder)
        {
            builder.ToTable("product_stocks");
            builder.HasKey(ps => ps.Id);
            builder.Property(ps => ps.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            builder.Property(ps => ps.ProductId).HasColumnName("product_id").IsRequired();
            builder.Property(ps => ps.Quantity).HasColumnName("quantity").HasDefaultValue(0);
            builder.Property(ps => ps.UpdatedAt).HasColumnName("updated_at").ValueGeneratedOnAddOrUpdate();
        }
    }
}
