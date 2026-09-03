using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

/// <summary>
/// Reads over the DCB event log.
/// </summary>
/// <remarks>
/// <para>
/// The streamed store gives each range variant its own file. They are grouped here because the DCB
/// variants differ only in the bound applied, and reading them side by side is what makes an
/// inconsistency between them visible.
/// </para>
/// <para>
/// A position bound is handed to <c>Inside</c>, which applies it to the tag rows the boundary is
/// resolved from, where the <c>(Tag, Position)</c> key can seek on it. A date bound cannot be: the
/// tag table carries no date, so those variants narrow the events the boundary already selected.
/// That needs no index of its own — the boundary has done the narrowing by the time the date is
/// applied, which is why the log carries no index on <c>CreatedDate</c>.
/// </para>
/// </remarks>
public static partial class DcbDbContextExtensions
{
    /// <summary>
    /// Gets the stored events inside a boundary, in position order.
    /// </summary>
    private static Task<List<DcbEventEntity>> GetEventEntities(this IDcbDbContext dcbDbContext,
        TagQuery query, Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default) =>
        dcbDbContext.Inside(query)
            .ApplyEventTypeFilter(eventTypeFilter)
            .InPositionOrder(cancellationToken);

    /// <summary>
    /// Gets the stored events inside a boundary from a position onwards, inclusive.
    /// </summary>
    private static Task<List<DcbEventEntity>> GetEventEntitiesFromPosition(this IDcbDbContext dcbDbContext,
        TagQuery query, long fromPosition, Type[]? eventTypeFilter = null,
        CancellationToken cancellationToken = default) =>
        dcbDbContext.Inside(query, fromPosition: fromPosition)
            .ApplyEventTypeFilter(eventTypeFilter)
            .InPositionOrder(cancellationToken);

    /// <summary>
    /// Gets the stored events inside a boundary up to a position, inclusive.
    /// </summary>
    private static Task<List<DcbEventEntity>> GetEventEntitiesUpToPosition(this IDcbDbContext dcbDbContext,
        TagQuery query, long upToPosition, Type[]? eventTypeFilter = null,
        CancellationToken cancellationToken = default) =>
        dcbDbContext.Inside(query, toPosition: upToPosition)
            .ApplyEventTypeFilter(eventTypeFilter)
            .InPositionOrder(cancellationToken);

    /// <summary>
    /// Gets the stored events inside a boundary between two positions, inclusive at both ends.
    /// </summary>
    private static Task<List<DcbEventEntity>> GetEventEntitiesBetweenPositions(this IDcbDbContext dcbDbContext,
        TagQuery query, long fromPosition, long toPosition, Type[]? eventTypeFilter = null,
        CancellationToken cancellationToken = default) =>
        dcbDbContext.Inside(query, fromPosition, toPosition)
            .ApplyEventTypeFilter(eventTypeFilter)
            .InPositionOrder(cancellationToken);

    /// <summary>
    /// Gets the stored events inside a boundary from a date onwards, inclusive.
    /// </summary>
    private static Task<List<DcbEventEntity>> GetEventEntitiesFromDate(this IDcbDbContext dcbDbContext,
        TagQuery query, DateTimeOffset fromDate, Type[]? eventTypeFilter = null,
        CancellationToken cancellationToken = default) =>
        dcbDbContext.Inside(query)
            .Where(eventEntity => eventEntity.CreatedDate >= fromDate)
            .ApplyEventTypeFilter(eventTypeFilter)
            .InPositionOrder(cancellationToken);

    /// <summary>
    /// Gets the stored events inside a boundary up to a date, inclusive.
    /// </summary>
    private static Task<List<DcbEventEntity>> GetEventEntitiesUpToDate(this IDcbDbContext dcbDbContext,
        TagQuery query, DateTimeOffset upToDate, Type[]? eventTypeFilter = null,
        CancellationToken cancellationToken = default) =>
        dcbDbContext.Inside(query)
            .Where(eventEntity => eventEntity.CreatedDate <= upToDate)
            .ApplyEventTypeFilter(eventTypeFilter)
            .InPositionOrder(cancellationToken);

    /// <summary>
    /// Gets the stored events inside a boundary between two dates, inclusive at both ends.
    /// </summary>
    private static Task<List<DcbEventEntity>> GetEventEntitiesBetweenDates(this IDcbDbContext dcbDbContext,
        TagQuery query, DateTimeOffset fromDate, DateTimeOffset toDate, Type[]? eventTypeFilter = null,
        CancellationToken cancellationToken = default) =>
        dcbDbContext.Inside(query)
            .Where(eventEntity => eventEntity.CreatedDate >= fromDate && eventEntity.CreatedDate <= toDate)
            .ApplyEventTypeFilter(eventTypeFilter)
            .InPositionOrder(cancellationToken);
}
