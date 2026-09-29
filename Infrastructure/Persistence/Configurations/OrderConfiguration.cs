using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    public class OrderConfiguration : IEntityTypeConfiguration<OrderEntity>
    {
        public void Configure(EntityTypeBuilder<OrderEntity> builder)
        {
            builder.ToTable("orders");
            builder.HasKey(o => o.Id);
            builder.Property(o => o.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            builder.Property(o => o.OrderNumber).HasColumnName("order_number").HasMaxLength(40).IsRequired();
            builder.Property(o => o.StatusId).HasColumnName("status_id").IsRequired();
            builder.Property(o => o.CustomerId).HasColumnName("customer_id").IsRequired();
            builder.Property(o => o.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
            builder.Property(o => o.Comment).HasColumnName("comment");
            builder.Property(o => o.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            builder.Property(o => o.UpdatedAt).HasColumnName("updated_at").ValueGeneratedOnAddOrUpdate();

            builder.HasOne(o => o.Status)
                .WithMany()
                .HasForeignKey(o => o.StatusId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(o => o.Customer)
                .WithMany()
                .HasForeignKey(o => o.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(o => o.CreatedByUser)
                .WithMany()
                .HasForeignKey(o => o.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(o => o.Items)
                .WithOne(i => i.Order)
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(o => o.StatusHistory)
                .WithOne(h => h.Order)
                .HasForeignKey(h => h.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
