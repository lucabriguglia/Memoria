using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Configurations;

public class DcbEventEntityConfiguration : IEntityTypeConfiguration<DcbEventEntity>
{
    public void Configure(EntityTypeBuilder<DcbEventEntity> builder)
    {
        builder
            .ToTable("dcb_events")
            .HasKey(e => e.EventId);

        builder.Property(e => e.EventId).HasMaxLength(255).IsRequired();
        builder.Property(e => e.EventType).HasMaxLength(255).IsRequired();
        builder.Property(e => e.GlobalPosition).IsRequired();
        builder.Property(e => e.Data).IsRequired();
        builder.Property(e => e.RecordedAt).IsRequired();

        builder
            .HasIndex(e => e.GlobalPosition)
            .IsUnique()
            .HasDatabaseName("UX_DcbEvents_GlobalPosition");

        builder
            .HasIndex(e => e.EventType)
            .HasDatabaseName("IX_DcbEvents_EventType");

        builder
            .HasMany(e => e.Tags)
            .WithOne(t => t.Event)
            .HasForeignKey(t => t.EventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
