using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Configurations;

/// <summary>
/// Maps <see cref="DcbSnapshotEntity"/> onto the <c>DcbSnapshots</c> table.
/// </summary>
public class DcbSnapshotEntityConfiguration : IEntityTypeConfiguration<DcbSnapshotEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<DcbSnapshotEntity> builder)
    {
        builder
            .ToTable(name: "DcbSnapshots")
            .HasKey(snapshot => snapshot.Id);

        // Kind, plus a store id capped at 255, plus a 32-character digest and two separators. Well
        // inside SQL Server's 900-byte limit for a clustered key even at the maximum.
        builder
            .Property(snapshot => snapshot.Id)
            .HasMaxLength(400)
            .IsRequired();

        builder
            .Property(snapshot => snapshot.SnapshotKind)
            .HasMaxLength(20)
            .IsRequired();

        builder
            .Property(snapshot => snapshot.StoreId)
            .HasMaxLength(255)
            .IsRequired();

        builder
            .Property(snapshot => snapshot.TagQuery)
            .IsRequired();

        builder
            .Property(snapshot => snapshot.ModelType)
            .HasMaxLength(255)
            .IsRequired();

        builder
            .Property(snapshot => snapshot.Data)
            .IsRequired();

        builder
            .Property(snapshot => snapshot.CreatedBy)
            .HasMaxLength(255);

        builder
            .Property(snapshot => snapshot.UpdatedBy)
            .HasMaxLength(255);

        // Serves "every snapshot of this model, whatever boundary produced it", which is what an
        // operator asks when a state looks wrong.
        builder
            .HasIndex(snapshot => new { snapshot.SnapshotKind, snapshot.StoreId })
            .HasDatabaseName("IX_DcbSnapshots_Kind_StoreId");
    }
}
