using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Memoria.EventSourcing.Domain;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

public static partial class DcbDbContextExtensions
{
    /// <summary>
    /// Gets the domain events inside a boundary, in position order.
    /// </summary>
    public static async Task<List<IEvent>> GetEvents(this IDcbDbContext dcbDbContext,
        TagQuery query, Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default) =>
        ToDomainEvents(await dcbDbContext.GetEventEntities(query, eventTypeFilter, cancellationToken));

    /// <summary>
    /// Gets the domain events inside a boundary from a position onwards, inclusive.
    /// </summary>
    public static async Task<List<IEvent>> GetEventsFromPosition(this IDcbDbContext dcbDbContext,
        TagQuery query, long fromPosition, Type[]? eventTypeFilter = null,
        CancellationToken cancellationToken = default) =>
        ToDomainEvents(await dcbDbContext.GetEventEntitiesFromPosition(query, fromPosition, eventTypeFilter,
            cancellationToken));

    /// <summary>
    /// Gets the domain events inside a boundary up to a position, inclusive.
    /// </summary>
    public static async Task<List<IEvent>> GetEventsUpToPosition(this IDcbDbContext dcbDbContext,
        TagQuery query, long upToPosition, Type[]? eventTypeFilter = null,
        CancellationToken cancellationToken = default) =>
        ToDomainEvents(await dcbDbContext.GetEventEntitiesUpToPosition(query, upToPosition, eventTypeFilter,
            cancellationToken));

    /// <summary>
    /// Gets the domain events inside a boundary between two positions, inclusive at both ends.
    /// </summary>
    public static async Task<List<IEvent>> GetEventsBetweenPositions(this IDcbDbContext dcbDbContext,
        TagQuery query, long fromPosition, long toPosition, Type[]? eventTypeFilter = null,
        CancellationToken cancellationToken = default) =>
        ToDomainEvents(await dcbDbContext.GetEventEntitiesBetweenPositions(query, fromPosition, toPosition,
            eventTypeFilter, cancellationToken));

    /// <summary>
    /// Gets the domain events inside a boundary from a date onwards, inclusive.
    /// </summary>
    public static async Task<List<IEvent>> GetEventsFromDate(this IDcbDbContext dcbDbContext,
        TagQuery query, DateTimeOffset fromDate, Type[]? eventTypeFilter = null,
        CancellationToken cancellationToken = default) =>
        ToDomainEvents(await dcbDbContext.GetEventEntitiesFromDate(query, fromDate, eventTypeFilter,
            cancellationToken));

    /// <summary>
    /// Gets the domain events inside a boundary up to a date, inclusive.
    /// </summary>
    public static async Task<List<IEvent>> GetEventsUpToDate(this IDcbDbContext dcbDbContext,
        TagQuery query, DateTimeOffset upToDate, Type[]? eventTypeFilter = null,
        CancellationToken cancellationToken = default) =>
        ToDomainEvents(await dcbDbContext.GetEventEntitiesUpToDate(query, upToDate, eventTypeFilter,
            cancellationToken));

    /// <summary>
    /// Gets the domain events inside a boundary between two dates, inclusive at both ends.
    /// </summary>
    public static async Task<List<IEvent>> GetEventsBetweenDates(this IDcbDbContext dcbDbContext,
        TagQuery query, DateTimeOffset fromDate, DateTimeOffset toDate, Type[]? eventTypeFilter = null,
        CancellationToken cancellationToken = default) =>
        ToDomainEvents(await dcbDbContext.GetEventEntitiesBetweenDates(query, fromDate, toDate, eventTypeFilter,
            cancellationToken));

    private static List<IEvent> ToDomainEvents(List<DcbEventEntity> eventEntities) =>
        eventEntities.Select(eventEntity => eventEntity.ToDomainEvent()).ToList();
}
