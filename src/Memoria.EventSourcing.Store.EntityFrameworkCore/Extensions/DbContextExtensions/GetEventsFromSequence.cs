using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

public static partial class IDomainDbContextExtensions
{
    /// <summary>
    /// Retrieves domain events from a specified stream starting from a specific sequence number onwards.
    /// </summary>
    /// <param name="domainDbContext">The domain database context.</param>
    /// <param name="streamId">The unique identifier for the event stream.</param>
    /// <param name="fromSequence">The minimum sequence number (inclusive).</param>
    /// <param name="eventTypeFilter">An optional array of event types to filter the results.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of domain events from the starting sequence onwards.</returns>
    /// <example>
    /// <code>
    /// var events = await context.GetEventsFromSequence(streamId, fromSequence);
    /// var filteredEvents = await context.GetEventsFromSequence(streamId, fromSequence, new[] { typeof(SomeEvent) });
    /// </code>
    /// </example>
    public static async Task<List<IEvent>> GetEventsFromSequence(this IDomainDbContext domainDbContext, IStreamId streamId, int fromSequence, Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default)
    {
        var eventEntities = await domainDbContext.GetEventEntitiesFromSequence(streamId, fromSequence, eventTypeFilter, cancellationToken);
        return eventEntities.Select(eventEntity => eventEntity.ToDomainEvent()).ToList();
    }
}
