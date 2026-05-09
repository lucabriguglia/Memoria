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
        IStreamId streamId, int fromSequence, Type[]? eventTypeFilter = null, string[]? eventPropertyFilter = null,
        CancellationToken cancellationToken = default)
    {
        // var filterEventTypes = eventTypeFilter is not null && eventTypeFilter.Length > 0;
        // if (!filterEventTypes)
        // {
        //     return await domainDbContext.Events.AsNoTracking()
        //         .Where(eventEntity => eventEntity.StreamId == streamId.Id && eventEntity.Sequence >= fromSequence)
        //         .OrderBy(eventEntity => eventEntity.Sequence)
        //         .ToListAsync(cancellationToken);
        // }
        //
        // var eventTypes = eventTypeFilter!
        //     .Select(eventType => TypeBindings.EventTypeBindings.FirstOrDefault(b => b.Value == eventType))
        //     .Select(b => b.Key).ToList();
        //
        // return await domainDbContext.Events.AsNoTracking()
        //     .Where(eventEntity => eventEntity.StreamId == streamId.Id && eventEntity.Sequence >= fromSequence && eventTypes.Contains(eventEntity.EventType))
        //     .OrderBy(eventEntity => eventEntity.Sequence)
        //     .ToListAsync(cancellationToken);


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
                        eventEntity.StreamId == streamId.Id && eventEntity.Sequence >= fromSequence
                                                            && eventEntity.Data.Contains(propertyFilter))
                    .OrderBy(eventEntity => eventEntity.Sequence)
                    .ToListAsync(cancellationToken);
            }

            return await domainDbContext.Events.AsNoTracking()
                .Where(eventEntity => eventEntity.StreamId == streamId.Id && eventEntity.Sequence >= fromSequence)
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
                    eventEntity.StreamId == streamId.Id && eventEntity.Sequence >= fromSequence &&
                    eventTypes2.Contains(eventEntity.EventType)
                    && eventEntity.Data.Contains(propertyFilter))
                .OrderBy(eventEntity => eventEntity.Sequence)
                .ToListAsync(cancellationToken);
        }

        var eventTypes = eventTypeFilter!
            .Select(eventType => TypeBindings.EventTypeBindings.FirstOrDefault(b => b.Value == eventType))
            .Select(b => b.Key).ToList();

        return await domainDbContext.Events.AsNoTracking()
            .Where(eventEntity => eventEntity.StreamId == streamId.Id && eventEntity.Sequence >= fromSequence &&
                                  eventTypes.Contains(eventEntity.EventType))
            .OrderBy(eventEntity => eventEntity.Sequence)
            .ToListAsync(cancellationToken);
    }
}