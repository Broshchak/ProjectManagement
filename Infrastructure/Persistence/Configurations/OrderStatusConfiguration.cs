using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    public class OrderStatusConfiguration : IEntityTypeConfiguration<OrderStatusEntity>
    {
        public void Configure(EntityTypeBuilder<OrderStatusEntity> builder)
        {
            builder.ToTable("order_statuses");
            builder.HasKey(os => os.Id);
            builder.Property(os => os.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            builder.Property(os => os.Code).HasColumnName("code").HasMaxLength(30).IsRequired();
            builder.Property(os => os.Name).HasColumnName("name").HasMaxLength(80).IsRequired();
            builder.Property(os => os.SortOrder).HasColumnName("sort_order").IsRequired();
            builder.Property(os => os.IsFinal).HasColumnName("is_final").HasDefaultValue(false);
            builder.HasIndex(os => os.Code).IsUnique();
            builder.HasIndex(os => os.SortOrder).IsUnique();
        }
    }
}
