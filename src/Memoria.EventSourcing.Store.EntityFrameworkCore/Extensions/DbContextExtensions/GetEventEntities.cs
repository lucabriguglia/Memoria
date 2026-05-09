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
        IStreamId streamId, Type[]? eventTypeFilter = null, string[]? eventPropertyFilter = null,
        CancellationToken cancellationToken = default)
    {
        var filterEventByProperty = eventPropertyFilter is not null && eventPropertyFilter.Length > 0;
        var filterEventsByType = eventTypeFilter is not null && eventTypeFilter.Length > 0;
        if (!filterEventsByType)
        {
            if (filterEventByProperty)
            {
                var eventProperty = eventPropertyFilter![0].Split("=");
                var propertyName = eventProperty[0];
                var propertyValue = eventProperty[1];
                var propertyFilter = $"\"{propertyName}\":\"{propertyValue}\"";

                return await domainDbContext.Events.AsNoTracking()
                    .Where(eventEntity =>
                        eventEntity.StreamId == streamId.Id
                        && eventEntity.Data.Contains(propertyFilter))
                    .OrderBy(eventEntity => eventEntity.Sequence)
                    .ToListAsync(cancellationToken);
            }

            return await domainDbContext.Events.AsNoTracking()
                .Where(eventEntity => eventEntity.StreamId == streamId.Id)
                .OrderBy(eventEntity => eventEntity.Sequence)
                .ToListAsync(cancellationToken);
        }

        if (filterEventByProperty)
        {
            var eventTypes2 = eventTypeFilter!
                .Select(eventType => TypeBindings.EventTypeBindings.FirstOrDefault(b => b.Value == eventType))
                .Select(b => b.Key).ToList();

            var eventProperty = eventPropertyFilter![0].Split("=");
            var propertyName = eventProperty[0];
            var propertyValue = eventProperty[1];
            var propertyFilter = $"\"{propertyName}\":\"{propertyValue}\"";

            return await domainDbContext.Events.AsNoTracking()
                .Where(eventEntity =>
                    eventEntity.StreamId == streamId.Id && eventTypes2.Contains(eventEntity.EventType)
                                                        && eventEntity.Data.Contains(propertyFilter))
                .OrderBy(eventEntity => eventEntity.Sequence)
                .ToListAsync(cancellationToken);
        }

        var eventTypes = eventTypeFilter!
            .Select(eventType => TypeBindings.EventTypeBindings.FirstOrDefault(b => b.Value == eventType))
            .Select(b => b.Key).ToList();

        return await domainDbContext.Events.AsNoTracking()
            .Where(eventEntity => eventEntity.StreamId == streamId.Id && eventTypes.Contains(eventEntity.EventType))
            .OrderBy(eventEntity => eventEntity.Sequence)
            .ToListAsync(cancellationToken);
    }
}