using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Configurations;

public class DcbEventTagEntityConfiguration : IEntityTypeConfiguration<DcbEventTagEntity>
{
    public void Configure(EntityTypeBuilder<DcbEventTagEntity> builder)
    {
        builder
            .ToTable("dcb_event_tags")
            .HasKey(t => new { t.EventId, t.TagKey });

        builder.Property(t => t.EventId).HasMaxLength(255).IsRequired();
        builder.Property(t => t.TagKey).HasMaxLength(64).IsRequired();
        builder.Property(t => t.TagValue).HasMaxLength(255).IsRequired();
        builder.Property(t => t.GlobalPosition).IsRequired();

        builder
            .HasIndex(t => new { t.TagKey, t.TagValue, t.GlobalPosition })
            .HasDatabaseName("IX_DcbEventTags_Lookup");
    }
}
