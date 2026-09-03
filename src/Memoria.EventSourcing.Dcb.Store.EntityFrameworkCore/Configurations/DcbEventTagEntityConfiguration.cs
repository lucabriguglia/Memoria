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

        // Entity Framework Core indexes the foreign key by convention, so Position carries an index
        // without one being declared here. It earns nothing on the read side — no read looks a tag up
        // by position, and the store offers no way to delete an event, so the cascade above is its
        // only possible caller. Measured over 400,000 tag rows it cost about 14% of the throughput of
        // a batched append, and a cascading delete was actually faster without it.
        //
        // Kept anyway. Removing a convention-created index needs a replacement convention set on the
        // context, which every consumer would inherit, and that is a lot of mechanism to shave a cost
        // that does not show up on the single-decision appends this store is built for. The trade
        // also turns around once the tag table stops fitting in memory and the cascade's scan has to
        // reach disk — which is exactly when someone would be deleting in bulk.
    }
}
