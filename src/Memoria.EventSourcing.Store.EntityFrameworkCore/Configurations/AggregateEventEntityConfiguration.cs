using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Configurations;

public class AggregateEventEntityConfiguration : IEntityTypeConfiguration<AggregateEventEntity>
{
    public void Configure(EntityTypeBuilder<AggregateEventEntity> builder)
    {
        builder
            .ToTable(name: "DomainAggregateEvents")
            .HasKey(aggregateEventEntity => new { aggregateEventEntity.AggregateId, aggregateEventEntity.EventId });

        builder
            .Property(aggregateEventEntity => aggregateEventEntity.AppliedDate)
            .IsRequired();

        builder
            .HasOne(aggregateEventEntity => aggregateEventEntity.Aggregate)
            .WithMany()
            .HasForeignKey(aggregateEventEntity => aggregateEventEntity.AggregateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(aggregateEventEntity => aggregateEventEntity.Event)
            .WithMany()
            .HasForeignKey(aggregateEventEntity => aggregateEventEntity.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasIndex(aggregateEventEntity => aggregateEventEntity.AggregateId)
            .HasDatabaseName("IX_AggregateEvents_AggregateId");
    }
}
