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
            builder.HasKey(ost => ost.Id);
            builder.Property(ost => ost.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            builder.Property(ost => ost.OrderId).HasColumnName("order_id").IsRequired();
            builder.Property(ost => ost.PreviousStatusId).HasColumnName("previous_status_id");
            builder.Property(ost => ost.NewStatusId).HasColumnName("new_status_id").IsRequired();
            builder.Property(ost => ost.ChangedByUserId).HasColumnName("changed_by_user_id").IsRequired();
            builder.Property(ost => ost.ChangedAt).HasColumnName("changed_at").HasDefaultValueSql("NOW()");
            builder.Property(ost => ost.Comment).HasColumnName("comment");
        }
    }
}
