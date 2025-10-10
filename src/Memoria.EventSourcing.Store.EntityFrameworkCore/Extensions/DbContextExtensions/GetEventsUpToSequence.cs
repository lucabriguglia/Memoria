using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

public static partial class IDomainDbContextExtensions
{
    /// <summary>
    /// Retrieves domain events from a specified stream up to and including a specific sequence number.
    /// </summary>
    /// <param name="domainDbContext">The domain database context.</param>
    /// <param name="streamId">The unique identifier for the event stream.</param>
    /// <param name="upToSequence">The maximum sequence number (inclusive).</param>
    /// <param name="eventTypeFilter">An optional array of event types to filter the results.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of domain events up to the specified sequence number.</returns>
    /// <example>
    /// <code>
    /// var events = await context.GetEventsUpToSequence(streamId, upToSequence);
    /// var filteredEvents = await context.GetEventsUpToSequence(streamId, upToSequence, new[] { typeof(SomeEvent) });
    /// </code>
    /// </example>
    public static async Task<List<IEvent>> GetEventsUpToSequence(this IDomainDbContext domainDbContext, IStreamId streamId, int upToSequence, Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default)
    {
        var eventEntities = await domainDbContext.GetEventEntitiesUpToSequence(streamId, upToSequence, eventTypeFilter, cancellationToken);
        return eventEntities.Select(eventEntity => eventEntity.ToDomainEvent()).ToList();
    }
}
