using Memoria.EventSourcing.Domain;
using Microsoft.EntityFrameworkCore;

namespace OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

public static partial class IDomainDbContextExtensions
{
    /// <summary>
    /// Retrieves the latest event sequence number for a specified stream.
    /// </summary>
    /// <param name="domainDbContext">The domain database context.</param>
    /// <param name="streamId">The unique identifier for the event stream.</param>
    /// <param name="eventTypeFilter">An optional array of event types to filter the results.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The latest sequence number in the stream.</returns>
    /// <example>
    /// <code>
    /// var sequence = await context.GetLatestEventSequence(streamId);
    /// </code>
    /// </example>
    public static async Task<int> GetLatestEventSequence(this IDomainDbContext domainDbContext, IStreamId streamId, Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default)
    {
        var filterEventTypes = eventTypeFilter is not null && eventTypeFilter.Length > 0;
        if (!filterEventTypes)
        {
            return await domainDbContext.Events.AsNoTracking()
                .Where(eventEntity => eventEntity.StreamId == streamId.Id)
                .MaxAsync(eventEntity => (int?)eventEntity.Sequence, cancellationToken) ?? 0;
        }

        var eventTypes = eventTypeFilter!
            .Select(eventType => TypeBindings.EventTypeBindings.FirstOrDefault(b => b.Value == eventType))
            .Select(b => b.Key).ToList();

        return await domainDbContext.Events.AsNoTracking()
            .Where(eventEntity => eventEntity.StreamId == streamId.Id && eventTypes.Contains(eventEntity.EventType))
            .MaxAsync(eventEntity => (int?)eventEntity.Sequence, cancellationToken) ?? 0;
    }
}
