using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Memoria.EventSourcing.Domain;
using Microsoft.EntityFrameworkCore;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

/// <summary>
/// Builds the query every DCB read narrows from.
/// </summary>
internal static class DcbEventQueryExtensions
{
    /// <summary>
    /// Selects the events inside a boundary — those carrying every tag of at least one of its
    /// groups.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The single place a boundary becomes SQL. Every read narrows from it, so both boundary shapes
    /// are understood here once rather than in each of them.
    /// </para>
    /// <para>
    /// A union is one <c>EXISTS</c> over an <c>IN</c>, rather than a join, so an event carrying two
    /// of the boundary's tags is still returned once — a join would return it per matching tag row
    /// and double-apply it in the fold. An intersection is one <c>EXISTS</c> per tag, chained: each
    /// is a semi-join and so cannot duplicate either, and each seeks the <c>(Tag, Position)</c>
    /// primary key, which means the cost grows with the number of tags in the group and no extra
    /// index is needed.
    /// </para>
    /// </remarks>
    public static IQueryable<DcbEventEntity> Inside(this IDcbDbContext dcbDbContext, TagQuery query)
    {
        var groups = query.TagGroups;
        var events = dcbDbContext.DcbEvents.AsNoTracking();

        if (groups.All(group => group.Count == 1))
        {
            var tags = groups.Select(group => group.Single().ToString()).ToList();

            return events.Where(eventEntity => eventEntity.Tags.Any(tagEntity => tags.Contains(tagEntity.Tag)));
        }

        // Only an intersection produces a group of more than one tag, and it produces exactly one
        // group. Single() rather than a loop over groups, so a query shape this store has not been
        // taught — a boundary mixing the two, which no factory builds today — fails loudly instead
        // of being translated as something wider than the caller asked for.
        return groups.Single().Aggregate(events, (narrowed, tag) =>
        {
            var required = tag.ToString();

            return narrowed.Where(eventEntity => eventEntity.Tags.Any(tagEntity => tagEntity.Tag == required));
        });
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
