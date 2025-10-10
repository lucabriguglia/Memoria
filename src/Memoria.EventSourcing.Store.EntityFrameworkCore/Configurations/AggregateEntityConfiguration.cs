using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Configurations;

public class AggregateEntityConfiguration : IEntityTypeConfiguration<AggregateEntity>
{
    public void Configure(EntityTypeBuilder<AggregateEntity> builder)
    {
        builder
            .ToTable(name: "DomainAggregates")
            .HasKey(aggregateEntity => aggregateEntity.Id);

        builder
            .Property(aggregateEntity => aggregateEntity.Id)
            .HasMaxLength(255)
            .IsRequired();

        builder
            .Property(aggregateEntity => aggregateEntity.StreamId)
            .HasMaxLength(255)
            .IsRequired();

        builder
            .Property(aggregateEntity => aggregateEntity.CreatedDate)
            .IsRequired();

        builder
            .Property(aggregateEntity => aggregateEntity.CreatedBy)
            .HasMaxLength(255);

        builder
            .Property(aggregateEntity => aggregateEntity.UpdatedDate)
            .IsRequired();

        builder
            .Property(aggregateEntity => aggregateEntity.UpdatedBy)
            .HasMaxLength(255);
    }
}
