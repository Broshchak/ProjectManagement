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
            builder.Property(o => o.OrderNumber).HasColumnName("order_number")
                .HasMaxLength(40).IsRequired();
            builder.Property(o => o.StatusId).HasColumnName("status_id").IsRequired();
            builder.Property(o => o.CustomerId).HasColumnName("customer_id").IsRequired();
            builder.Property(o => o.CreatedByUserId).HasColumnName("created_by_user_id")
                .IsRequired();
            builder.Property(o => o.Comment).HasColumnName("comment").HasColumnType("text");
            builder.Property(o => o.CreatedAt).HasColumnName("created_at")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("NOW()").ValueGeneratedOnAdd();
            builder.Property(o => o.UpdatedAt).HasColumnName("updated_at")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("NOW()").ValueGeneratedOnAddOrUpdate();

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

            builder.HasIndex(o => o.OrderNumber)
                .IsUnique().HasDatabaseName("orders_order_number_key");
            builder.HasIndex(o => o.StatusId).HasDatabaseName("ix_orders_status_id");
            builder.HasIndex(o => o.CustomerId).HasDatabaseName("ix_orders_customer_id");
            builder.HasIndex(o => o.CreatedAt).HasDatabaseName("ix_orders_created_at");
            builder.HasIndex(o => o.CreatedByUserId)
                .HasDatabaseName("ix_orders_created_by_user_id");
        }
    }
}
