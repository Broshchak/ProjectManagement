using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class OrderWorkflowConfiguration : IEntityTypeConfiguration<OrderWorkflowEntity>
{
    public void Configure(EntityTypeBuilder<OrderWorkflowEntity> builder)
    {
        builder.ToTable("order_workflow");
        builder.HasKey(row => row.Id);
        builder.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(row => row.Revision).HasColumnName("revision");
        builder.Property(row => row.DraftVersion).HasColumnName("draft_version");
        builder.Property(row => row.Published).HasColumnName("published").HasColumnType("jsonb");
        builder.Property(row => row.Draft).HasColumnName("draft").HasColumnType("jsonb");
    }
}
