using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

/// <summary>
/// Provides extension methods for IDomainDbContext.
/// </summary>
public static partial class IDomainDbContextExtensions
{
    /// <summary>
    /// Retrieves domain events from a specified date for a given stream.
    /// </summary>
    /// <param name="domainDbContext">The domain database context.</param>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="fromDate">The start date.</param>
    /// <param name="eventTypeFilter">Optional filter for event types.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of domain events.</returns>
    /// <example>
    /// <code>
    /// var events = await context.GetEventsFromDate(streamId, fromDate);
    /// var filteredEvents = await context.GetEventsFromDate(streamId, fromDate, new[] { typeof(SomeEvent) });
    /// </code>
    /// </example>
    public static async Task<List<IEvent>> GetEventsFromDate(this IDomainDbContext domainDbContext, IStreamId streamId, DateTimeOffset fromDate, Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default)
    {
        var eventEntities = await domainDbContext.GetEventEntitiesFromDate(streamId, fromDate, eventTypeFilter, cancellationToken);
        return eventEntities.Select(eventEntity => eventEntity.ToDomainEvent()).ToList();
    }
}
