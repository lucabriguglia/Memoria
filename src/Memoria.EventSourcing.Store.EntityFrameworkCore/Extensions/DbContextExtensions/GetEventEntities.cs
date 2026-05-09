using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

public static partial class IDomainDbContextExtensions
{
    /// <summary>
    /// Retrieves all event entities from a specified stream, with optional filtering by event types.
    /// </summary>
    /// <param name="domainDbContext">The domain database context.</param>
    /// <param name="streamId">The unique identifier for the event stream.</param>
    /// <param name="eventTypeFilter">An optional array of event types to filter the results.</param>
    /// <param name="eventPropertyFilter">An optional array of event properties to filter the results.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of event entities from the stream.</returns>
    /// <example>
    /// <code>
    /// var entities = await context.GetEventEntities(streamId);
    /// var filteredEntities = await context.GetEventEntities(streamId, new[] { typeof(SomeEvent) });
    /// </code>
    /// </example>
    public static async Task<List<EventEntity>> GetEventEntities(this IDomainDbContext domainDbContext,
        IStreamId streamId, Type[]? eventTypeFilter = null, IDictionary<string, string>? eventPropertyFilter = null,
        CancellationToken cancellationToken = default)
    {
        var query = domainDbContext.Events.AsNoTracking().Where(eventEntity => eventEntity.StreamId == streamId.Id);

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