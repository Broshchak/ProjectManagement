using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    public class ProductPriceConfiguration : IEntityTypeConfiguration<ProductPriceEntity>
    {
        public void Configure(EntityTypeBuilder<ProductPriceEntity> builder)
        {
            builder.ToTable("product_prices");
            builder.HasKey(pp => pp.Id);
            builder.Property(pp => pp.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            builder.Property(pp => pp.ProductId).HasColumnName("product_id").IsRequired();
            builder.Property(pp => pp.Price).HasColumnName("price").HasColumnType("numeric(12, 2)").IsRequired();
            builder.Property(pp => pp.CurrencyCode).HasColumnName("currency_code").HasMaxLength(3).HasDefaultValue("UAH");
            builder.Property(pp => pp.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        }
    }
}
