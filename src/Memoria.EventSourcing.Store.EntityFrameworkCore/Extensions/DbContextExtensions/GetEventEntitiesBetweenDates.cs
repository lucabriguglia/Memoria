using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

/// <summary>
/// Provides extension methods for IDomainDbContext.
/// </summary>
public static partial class IDomainDbContextExtensions
{
    /// <summary>
    /// Retrieves event entities between specified dates for a given stream.
    /// </summary>
    /// <param name="domainDbContext">The domain database context.</param>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="fromDate">The start date.</param>
    /// <param name="toDate">The end date.</param>
    /// <param name="eventTypeFilter">Optional filter for event types.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of event entities.</returns>
    /// <example>
    /// <code>
    /// var entities = await context.GetEventEntitiesBetweenDates(streamId, fromDate, toDate);
    /// var filteredEntities = await context.GetEventEntitiesBetweenDates(streamId, fromDate, toDate, new[] { typeof(SomeEvent) });
    /// </code>
    /// </example>
    public static async Task<List<EventEntity>> GetEventEntitiesBetweenDates(this IDomainDbContext domainDbContext, IStreamId streamId, DateTimeOffset fromDate, DateTimeOffset toDate, Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default)
    {
        var filterEventTypes = eventTypeFilter is not null && eventTypeFilter.Length > 0;
        if (!filterEventTypes)
        {
            return await domainDbContext.Events.AsNoTracking()
                .Where(eventEntity => eventEntity.StreamId == streamId.Id && eventEntity.CreatedDate >= fromDate && eventEntity.CreatedDate <= toDate)
                .OrderBy(eventEntity => eventEntity.Sequence)
                .ToListAsync(cancellationToken);
        }

        var eventTypes = eventTypeFilter!
            .Select(eventType => TypeBindings.EventTypeBindings.FirstOrDefault(b => b.Value == eventType))
            .Select(b => b.Key).ToList();

        return await domainDbContext.Events.AsNoTracking()
            .Where(eventEntity => eventEntity.StreamId == streamId.Id && eventEntity.CreatedDate >= fromDate && eventEntity.CreatedDate <= toDate && eventTypes.Contains(eventEntity.EventType))
            .OrderBy(eventEntity => eventEntity.Sequence)
            .ToListAsync(cancellationToken);
    }
}
