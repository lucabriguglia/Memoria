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

        // No secondary index, deliberately. Every read reaches this table by position, having already
        // resolved its boundary through the DcbEventTags key, so an index on event type or date is
        // maintained on every append and chosen by nothing — and an index on event type is worse than
        // that, because the optimiser will prefer it to the tag semi-join and lose badly. See
        // SchemaTests.The_log_carries_no_index_the_primary_keys_do_not_already_serve for the numbers.
    }
}
