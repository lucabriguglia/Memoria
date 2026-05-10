using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

public static partial class IDomainDbContextExtensions
{
    /// <summary>
    /// Retrieves a list of event entities from the specified stream starting from a given sequence number.
    /// </summary>
    /// <param name="domainDbContext">The domain database context.</param>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="fromSequence">The starting sequence number (inclusive).</param>
    /// <param name="eventTypeFilter">Optional array of event types to filter by.</param>
    /// <param name="eventPropertyFilter">Optional array of event properties to filter by.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A list of event entities ordered by sequence.</returns>
    /// <example>
    /// <code>
    /// var entities = await context.GetEventEntitiesFromSequence(streamId, fromSequence);
    /// var filteredEntities = await context.GetEventEntitiesFromSequence(streamId, fromSequence, new[] { typeof(SomeEvent) });
    /// </code>
    /// </example>
    public static async Task<List<EventEntity>> GetEventEntitiesFromSequence(this IDomainDbContext domainDbContext,
        IStreamId streamId, int fromSequence, Type[]? eventTypeFilter = null, IDictionary<string, string>? eventPropertyFilter = null,
        CancellationToken cancellationToken = default)
    {
        var query = domainDbContext.Events.AsNoTracking().Where(eventEntity =>
            eventEntity.StreamId == streamId.Id && eventEntity.Sequence >= fromSequence);

        if (eventTypeFilter is { Length: > 0 })
        {
            var eventTypes = eventTypeFilter
                .Select(eventType => TypeBindings.EventTypeBindings.FirstOrDefault(b => b.Value == eventType))
                .Select(b => b.Key).ToList();

            query = query.Where(eventEntity => eventTypes.Contains(eventEntity.EventType));
        }

        if (eventPropertyFilter is { Count: > 0 })
        {
            foreach (var filter in eventPropertyFilter)
            {
                var propertyFilter = $"\"{filter.Key}\":\"{filter.Value}\"";
                query = query.Where(eventEntity => eventEntity.Data.Contains(propertyFilter));
            }
        }

        return await query
            .OrderBy(eventEntity => eventEntity.Sequence)
            .ToListAsync(cancellationToken);
    }
}