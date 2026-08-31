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
    /// Selects the events inside a boundary — those carrying at least one of its tags.
    /// </summary>
    /// <remarks>
    /// Expressed as a single <c>Any</c> over the tag collection rather than a join, so an event
    /// carrying two of the boundary's tags is still returned once. A join would return it per
    /// matching tag row and double-apply it in the fold.
    /// </remarks>
    public static IQueryable<DcbEventEntity> Inside(this IDcbDbContext dcbDbContext, TagQuery query)
    {
        var tags = query.Tags.Select(tag => tag.ToString()).ToList();

        return dcbDbContext.DcbEvents.AsNoTracking()
            .Where(eventEntity => eventEntity.Tags.Any(tagEntity => tags.Contains(tagEntity.Tag)));
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
