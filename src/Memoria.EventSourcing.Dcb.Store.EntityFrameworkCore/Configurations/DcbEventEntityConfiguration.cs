using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Configurations;

/// <summary>
/// Maps <see cref="DcbEventEntity"/> onto the <c>DcbEvents</c> table.
/// </summary>
public class DcbEventEntityConfiguration : IEntityTypeConfiguration<DcbEventEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<DcbEventEntity> builder)
    {
        builder
            .ToTable(name: "DcbEvents")
            .HasKey(eventEntity => eventEntity.Position);

        // The database assigns it. Positions are dense enough to order by and deliberately not
        // guaranteed gap-free; see DcbEventEntity.Position.
        builder
            .Property(eventEntity => eventEntity.Position)
            .ValueGeneratedOnAdd();

        builder
            .Property(eventEntity => eventEntity.EventType)
            .HasMaxLength(255)
            .IsRequired();

        builder
            .Property(eventEntity => eventEntity.Data)
            .IsRequired();

        builder
            .Property(eventEntity => eventEntity.CreatedDate)
            .IsRequired();

        builder
            .Property(eventEntity => eventEntity.CreatedBy)
            .HasMaxLength(255);

        builder
            .HasIndex(eventEntity => eventEntity.EventType)
            .HasDatabaseName("IX_DcbEvents_EventType");

        // Serves the from/up-to/between-date reads, which would otherwise scan the whole log. The
        // streamed store's equivalent leads with StreamId; there is no such column to lead with
        // here, because a DCB read is narrowed by tag rather than by stream.
        builder
            .HasIndex(eventEntity => eventEntity.CreatedDate)
            .HasDatabaseName("IX_DcbEvents_CreatedDate");
    }
}
