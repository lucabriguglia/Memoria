using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Configurations;

public class ProjectionEntityConfiguration : IEntityTypeConfiguration<ProjectionEntity>
{
    public void Configure(EntityTypeBuilder<ProjectionEntity> builder)
    {
        builder
            .ToTable(name: "DomainProjections")
            .HasKey(projectionEntity => projectionEntity.Id);

        builder
            .Property(projectionEntity => projectionEntity.Id)
            .HasMaxLength(255)
            .IsRequired();

        builder
            .Property(projectionEntity => projectionEntity.StreamId)
            .HasMaxLength(255)
            .IsRequired();

        builder
            .Property(projectionEntity => projectionEntity.CreatedDate)
            .IsRequired();

        builder
            .Property(projectionEntity => projectionEntity.CreatedBy)
            .HasMaxLength(255);

        builder
            .Property(projectionEntity => projectionEntity.UpdatedDate)
            .IsRequired();

        builder
            .Property(projectionEntity => projectionEntity.UpdatedBy)
            .HasMaxLength(255);
    }
}
