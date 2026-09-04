using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Memoria.EventSourcing.Domain;
using Microsoft.EntityFrameworkCore;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

/// <summary>
/// Builds the query every DCB read narrows from.
/// </summary>
/// <remarks>
/// <para>
/// A union's tags reach the database as a collection, and what a provider makes of that differs.
/// SQL Server expands it to ordinary parameters — <c>Tag = @p0</c> for one tag, <c>IN (@p0, @p1)</c>
/// for two. Npgsql keeps it as a single array parameter, <c>Tag = ANY(@tags)</c>, at every
/// cardinality including one, and PostgreSQL cannot estimate the selectivity of an array parameter:
/// on a table it has no statistics for it can assume one row on each side, choose a nested loop semi
/// join, and apply the position match as a filter rather than an index condition.
/// </para>
/// <para>
/// Left alone deliberately. Rewriting the predicate to an equality per tag was built and measured: it
/// is worth about 10% on a read whose statistics are stale and nothing once they exist, which did not
/// pay for the expression-building it needed. If that estimate ever does become the problem, the knob
/// is <c>EF.Constant</c> or Npgsql's parameterised-collection option, and neither needs a change
/// here. See "Why the harness runs ANALYZE on PostgreSQL" in <c>benchmarks/README.md</c>.
/// </para>
/// </remarks>
internal static class DcbEventQueryExtensions
{
    /// <summary>
    /// Selects the positions inside a boundary — those of the events carrying every tag of at least
    /// one of its groups — reading the tag table alone.
    /// </summary>
    /// <param name="dcbDbContext">The context.</param>
    /// <param name="query">The consistency boundary.</param>
    /// <param name="fromPosition">An optional inclusive lower bound.</param>
    /// <param name="toPosition">An optional inclusive upper bound.</param>
    /// <remarks>
    /// <para>
    /// The single place a boundary becomes SQL. Every read narrows from it, so both boundary shapes
    /// are understood here once rather than in each of them.
    /// </para>
    /// <para>
    /// A union is one <c>IN</c> over the tags, rather than a join, so an event carrying two of the
    /// boundary's tags contributes its position once — a join would return it per matching tag row
    /// and double-apply it in the fold. An intersection anchors on one tag and requires the rest with
    /// one <c>EXISTS</c> each: each is a semi-join and so cannot duplicate either, and each seeks the
    /// <c>(Tag, Position)</c> primary key, which means the cost grows with the number of tags in the
    /// group and no extra index is needed.
    /// </para>
    /// <para>
    /// The position bounds are applied <em>here</em>, on the tag rows, rather than on the events the
    /// caller ends up with. Both are correct — the two are equated by the semi-join — but only this
    /// one puts the bound where the <c>(Tag, Position)</c> key can seek on it instead of leaving the
    /// optimizer to propagate it across the join. That matters most for the read after a snapshot,
    /// which asks for a suffix of a boundary whose whole history may be long.
    /// </para>
    /// </remarks>
    public static IQueryable<long> PositionsInside(this IDcbDbContext dcbDbContext, TagQuery query,
        long? fromPosition = null, long? toPosition = null)
    {
        var groups = query.TagGroups;
        var tagRows = dcbDbContext.DcbEventTags.AsNoTracking();

        if (fromPosition is { } from)
        {
            tagRows = tagRows.Where(tagEntity => tagEntity.Position >= from);
        }

        if (toPosition is { } to)
        {
            tagRows = tagRows.Where(tagEntity => tagEntity.Position <= to);
        }

        if (groups.All(group => group.Count == 1))
        {
            var tags = groups.Select(group => group.Single().ToString()).ToList();

            // Distinct because an event carrying two of the boundary's tags has a row for each. Every
            // caller today consumes this as a semi-join or an aggregate, where Entity Framework Core
            // drops it as redundant — but the set this returns is what the name promises, not what
            // the current callers happen to tolerate.
            return tagRows.Where(tagEntity => tags.Contains(tagEntity.Tag))
                .Select(tagEntity => tagEntity.Position)
                .Distinct();
        }

        // Only an intersection produces a group of more than one tag, and it produces exactly one
        // group. Single() rather than a loop over groups, so a query shape this store has not been
        // taught — a boundary mixing the two, which no factory builds today — fails loudly instead
        // of being translated as something wider than the caller asked for.
        var required = groups.Single().Select(tag => tag.ToString()).ToList();
        var anchor = required[0];

        // Anchored on one tag and narrowed by the rest, rather than narrowed from every event: the
        // anchor is a seek on the tag key, and each remaining tag is another seek on the same key.
        // No Distinct needed, unlike the union above — (Tag, Position) is the primary key, so the
        // anchor matches at most one row per position.
        var anchored = required.Skip(1).Aggregate(
            tagRows.Where(tagEntity => tagEntity.Tag == anchor),
            (narrowed, tag) => narrowed.Where(tagEntity => dcbDbContext.DcbEventTags
                .Any(other => other.Tag == tag && other.Position == tagEntity.Position)));

        return anchored.Select(tagEntity => tagEntity.Position);
    }

    /// <summary>
    /// Selects the events inside a boundary, optionally bounded by position.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One correlated <c>EXISTS</c> over the event's tags, so an event inside the boundary is
    /// returned exactly once however many of the boundary's tags it carries — a join would return it
    /// per matching tag row and the fold would apply it twice.
    /// </para>
    /// <para>
    /// A position bound goes <em>inside</em> the <c>EXISTS</c> rather than onto the events it selects.
    /// Both are correct, because the subquery equates the two positions, but only this one gives the
    /// <c>(Tag, Position)</c> key both halves of a seek instead of leaving the engine to infer the
    /// second. That is the read after a snapshot, which asks for a suffix of a boundary whose whole
    /// history may be long.
    /// </para>
    /// <para>
    /// Written as four explicit cases rather than one composed predicate: an unbounded read is the
    /// common one and has to stay a bare <c>EXISTS</c>, because folding the bounds in as always-true
    /// comparisons measurably costs it. See the note on small boundaries in <c>benchmarks/README.md</c>.
    /// </para>
    /// </remarks>
    public static IQueryable<DcbEventEntity> Inside(this IDcbDbContext dcbDbContext, TagQuery query,
        long? fromPosition = null, long? toPosition = null)
    {
        var groups = query.TagGroups;
        var events = dcbDbContext.DcbEvents.AsNoTracking();

        if (groups.All(group => group.Count == 1))
        {
            var tags = groups.Select(group => group.Single().ToString()).ToList();

            return (fromPosition, toPosition) switch
            {
                (null, null) => events.Where(eventEntity =>
                    eventEntity.Tags.Any(tagEntity => tags.Contains(tagEntity.Tag))),

                ({ } from, null) => events.Where(eventEntity =>
                    eventEntity.Tags.Any(tagEntity => tags.Contains(tagEntity.Tag)
                                                      && tagEntity.Position >= from)),

                (null, { } to) => events.Where(eventEntity =>
                    eventEntity.Tags.Any(tagEntity => tags.Contains(tagEntity.Tag)
                                                      && tagEntity.Position <= to)),

                ({ } from, { } to) => events.Where(eventEntity =>
                    eventEntity.Tags.Any(tagEntity => tags.Contains(tagEntity.Tag)
                                                      && tagEntity.Position >= from
                                                      && tagEntity.Position <= to))
            };
        }

        // Only an intersection produces a group of more than one tag, and it produces exactly one
        // group. Single() rather than a loop over groups, so a query shape this store has not been
        // taught — a boundary mixing the two, which no factory builds today — fails loudly instead
        // of being translated as something wider than the caller asked for.
        //
        // Each tag is its own EXISTS, so each is a semi-join that cannot duplicate either. The bound
        // rides on the events here rather than inside every one of them: an intersection is already
        // seeking one tag at a time, and repeating the bound per tag buys nothing.
        var narrowedEvents = groups.Single().Aggregate(events, (narrowed, tag) =>
        {
            var required = tag.ToString();

            return narrowed.Where(eventEntity => eventEntity.Tags.Any(tagEntity => tagEntity.Tag == required));
        });

        if (fromPosition is { } lower)
        {
            narrowedEvents = narrowedEvents.Where(eventEntity => eventEntity.Position >= lower);
        }

        if (toPosition is { } upper)
        {
            narrowedEvents = narrowedEvents.Where(eventEntity => eventEntity.Position <= upper);
        }

        return narrowedEvents;
    }

    /// <summary>
    /// Narrows a query to the given event types.
    /// </summary>
    public static IQueryable<DcbEventEntity> ApplyEventTypeFilter(
        this IQueryable<DcbEventEntity> query, Type[]? eventTypeFilter)
    {
        if (eventTypeFilter is not { Length: > 0 })
        {
            return query;
        }

        var bindingKeysByType = TypeBindings.GetEventBindingKeysByType();

        // An unregistered type contributes a null key, which matches nothing — the same behaviour
        // the streamed store has, so a filter naming a type nobody registered narrows to empty
        // rather than silently widening.
        var eventTypes = eventTypeFilter.Select(bindingKeysByType.GetValueOrDefault).ToList();

        return query.Where(eventEntity => eventTypes.Contains(eventEntity.EventType));
    }

    /// <summary>
    /// Orders a query by position and materialises it.
    /// </summary>
    public static Task<List<DcbEventEntity>> InPositionOrder(
        this IQueryable<DcbEventEntity> query, CancellationToken cancellationToken) =>
        query.OrderBy(eventEntity => eventEntity.Position).ToListAsync(cancellationToken);
}
