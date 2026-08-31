using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Configurations;

/// <summary>
/// Maps <see cref="DcbTagHeadEntity"/> onto the <c>DcbTagHeads</c> table.
/// </summary>
/// <param name="tagCollation">
/// The collation to apply to the tag column. It must match the one on <c>DcbEventTags</c>, or the
/// two tables would disagree about which tags are the same tag.
/// </param>
public class DcbTagHeadEntityConfiguration(string? tagCollation = null)
    : IEntityTypeConfiguration<DcbTagHeadEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<DcbTagHeadEntity> builder)
    {
        builder
            .ToTable(name: "DcbTagHeads")
            .HasKey(head => head.Tag);

        var tag = builder
            .Property(head => head.Tag)
            .HasMaxLength(255)
            .IsRequired();

        if (tagCollation is not null)
        {
            tag.UseCollation(tagCollation);
        }

        builder
            .Property(head => head.Token)
            .IsConcurrencyToken()
            .IsRequired();
    }
}
