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
            builder.Property(os => os.SortOrder).HasColumnName("sort_order")
                .HasColumnType("smallint").IsRequired();
            builder.Property(os => os.IsFinal).HasColumnName("is_final")
                .IsRequired().HasDefaultValue(false);
            builder.Property(os => os.CanEditItems).HasColumnName("can_edit_items");
            builder.Property(os => os.Category).HasColumnName("category").HasMaxLength(16);

            builder.HasIndex(os => os.Code).IsUnique()
                .HasDatabaseName("order_statuses_code_key");
        }
    }
}
