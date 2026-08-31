using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Configurations;

/// <summary>
/// Maps <see cref="DcbEventTagEntity"/> onto the <c>DcbEventTags</c> table.
/// </summary>
/// <param name="tagCollation">
/// The collation to apply to the tag column, or null to leave the database default in place. See
/// <see cref="DcbDbContext.TagCollation"/> for why this is not optional in practice.
/// </param>
public class DcbEventTagEntityConfiguration(string? tagCollation = null)
    : IEntityTypeConfiguration<DcbEventTagEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<DcbEventTagEntity> builder)
    {
        builder
            .ToTable(name: "DcbEventTags")
            // Tag first: every read narrows by tag and only then by position or date, so the
            // primary key is also the serving index and no secondary index on Tag is needed.
            .HasKey(tagEntity => new { tagEntity.Tag, tagEntity.Position });

        var tag = builder
            .Property(tagEntity => tagEntity.Tag)
            .HasMaxLength(255)
            .IsRequired();

        if (tagCollation is not null)
        {
            tag.UseCollation(tagCollation);
        }

        builder
            .HasOne(tagEntity => tagEntity.Event)
            .WithMany(eventEntity => eventEntity.Tags)
            .HasForeignKey(tagEntity => tagEntity.Position)
            .OnDelete(DeleteBehavior.Cascade);

        // The primary key leads with Tag, so it cannot serve a lookup by position alone — which the
        // cascade and the read-back of an event's own tags both need.
        builder
            .HasIndex(tagEntity => tagEntity.Position)
            .HasDatabaseName("IX_DcbEventTags_Position");
    }
}
