using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    public class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistoryEntity>
    {
        public void Configure(EntityTypeBuilder<OrderStatusHistoryEntity> builder)
        {
            builder.ToTable("order_status_history");
            builder.HasKey(h => h.Id);
            builder.Property(h => h.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            builder.Property(h => h.OrderId).HasColumnName("order_id").IsRequired();
            builder.Property(h => h.PreviousStatusId).HasColumnName("previous_status_id");
            builder.Property(h => h.NewStatusId).HasColumnName("new_status_id").IsRequired();
            builder.Property(h => h.ChangedByUserId).HasColumnName("changed_by_user_id")
                .IsRequired();
            builder.Property(h => h.ChangedAt).HasColumnName("changed_at")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("NOW()").ValueGeneratedOnAdd();
            builder.Property(h => h.Comment).HasColumnName("comment").HasColumnType("text");

            builder.HasOne(h => h.Order)
                .WithMany(o => o.StatusHistory)
                .HasForeignKey(h => h.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(h => h.PreviousStatus)
                .WithMany()
                .HasForeignKey(h => h.PreviousStatusId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            builder.HasOne(h => h.NewStatus)
                .WithMany()
                .HasForeignKey(h => h.NewStatusId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(h => h.ChangedByUser)
                .WithMany()
                .HasForeignKey(h => h.ChangedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(h => h.OrderId)
                .HasDatabaseName("ix_order_status_history_order_id");
            builder.HasIndex(h => h.ChangedAt)
                .HasDatabaseName("ix_order_status_history_changed_at");
        }
    }
}
