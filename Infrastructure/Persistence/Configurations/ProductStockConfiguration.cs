using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    public class ProductStockConfiguration : IEntityTypeConfiguration<ProductStockEntity>
    {
        public void Configure(EntityTypeBuilder<ProductStockEntity> builder)
        {
            builder.ToTable("product_stocks", t => t.HasCheckConstraint(
                "ck_product_stocks_quantity", "quantity >= 0"));
            builder.HasKey(ps => ps.Id);
            builder.Property(ps => ps.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            builder.Property(ps => ps.ProductId).HasColumnName("product_id").IsRequired();
            builder.Property(ps => ps.Quantity).HasColumnName("quantity")
                .IsRequired().HasDefaultValue(0);
            builder.Property(ps => ps.UpdatedAt).HasColumnName("updated_at")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("NOW()").ValueGeneratedOnAddOrUpdate();

            builder.HasOne(ps => ps.Product)
                .WithOne(p => p.Stock)
                .HasForeignKey<ProductStockEntity>(ps => ps.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(ps => ps.ProductId).IsUnique()
                .HasDatabaseName("product_stocks_product_id_key");
        }
    }
}
