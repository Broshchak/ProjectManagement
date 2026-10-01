using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItemEntity>
    {
        public void Configure(EntityTypeBuilder<OrderItemEntity> builder)
        {
            builder.ToTable("order_items");
            builder.HasKey(oi => oi.Id);
            builder.Property(oi => oi.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            builder.Property(oi => oi.OrderId).HasColumnName("order_id").IsRequired();
            builder.Property(oi => oi.ProductId).HasColumnName("product_id").IsRequired();
            builder.Property(oi => oi.Quantity).HasColumnName("quantity").IsRequired();
            builder.Property(oi => oi.UnitPrice).HasColumnName("unit_price").HasColumnType("numeric(12, 2)").IsRequired();
            builder.Property(oi => oi.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            builder.Property(oi => oi.UpdatedAt).HasColumnName("updated_at").ValueGeneratedOnAddOrUpdate();

            builder.HasOne(oi => oi.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(oi => oi.Product)
                .WithMany()
                .HasForeignKey(oi => oi.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
