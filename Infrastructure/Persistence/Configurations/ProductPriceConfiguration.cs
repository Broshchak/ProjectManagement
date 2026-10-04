using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    public class ProductPriceConfiguration : IEntityTypeConfiguration<ProductPriceEntity>
    {
        public void Configure(EntityTypeBuilder<ProductPriceEntity> builder)
        {
            builder.ToTable("product_prices", t => t.HasCheckConstraint(
                "ck_product_prices_price", "price >= 0"));
            builder.HasKey(pp => pp.Id);
            builder.Property(pp => pp.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            builder.Property(pp => pp.ProductId).HasColumnName("product_id").IsRequired();
            builder.Property(pp => pp.Price).HasColumnName("price")
                .HasColumnType("numeric(12, 2)").IsRequired();
            builder.Property(pp => pp.CurrencyCode).HasColumnName("currency_code")
                .HasColumnType("char(3)").IsRequired().HasDefaultValue("UAH");
            builder.Property(pp => pp.CreatedAt).HasColumnName("created_at")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("NOW()").ValueGeneratedOnAdd();

            builder.HasOne(pp => pp.Product)
                .WithOne(p => p.Price)
                .HasForeignKey<ProductPriceEntity>(pp => pp.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(pp => pp.ProductId).IsUnique()
                .HasDatabaseName("product_prices_product_id_key");
        }
    }
}
