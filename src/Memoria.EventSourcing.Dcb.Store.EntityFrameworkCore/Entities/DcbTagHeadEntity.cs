// ReSharper disable EntityFramework.ModelValidation.UnlimitedStringLength

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;

/// <summary>
/// One row per distinct tag ever appended under or conditioned on. It carries no domain data: it
/// exists so that two appends whose boundaries overlap contend on the same rows, and two whose
/// boundaries are disjoint do not.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately holds no position. The truth about how far a boundary has moved is
/// <c>MAX(Position)</c> over <see cref="DcbEventTagEntity"/>; duplicating it here would mean writing
/// a value that is only known after the database has assigned the new positions, which would split
/// the append across two round trips for no gain.
/// </para>
/// <para>
/// The row grows unboundedly in tag cardinality — one row per student, per order, forever. It is
/// small, and it is the price of contention that follows the boundary rather than the whole store.
/// </para>
/// </remarks>
public class DcbTagHeadEntity
{
    /// <summary>
    /// Gets or sets the tag, in its canonical <c>{key}:{value}</c> form.
    /// </summary>
    public string Tag { get; set; } = null!;

    /// <summary>
    /// Gets or sets the concurrency token, replaced by every append that touches this tag.
    /// </summary>
    /// <remarks>
    /// An application-assigned <see cref="Guid"/> rather than a provider-native <c>rowversion</c> or
    /// <c>xmin</c>, so that one implementation is correct on SQL Server, PostgreSQL and SQLite alike
    /// and the base package needs no provider reference.
    /// </remarks>
    public Guid Token { get; set; }
}
