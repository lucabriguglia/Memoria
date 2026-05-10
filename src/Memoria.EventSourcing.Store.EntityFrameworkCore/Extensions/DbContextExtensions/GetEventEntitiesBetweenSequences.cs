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
    /// Retrieves event entities between specified sequences for a given stream.
    /// </summary>
    /// <param name="domainDbContext">The domain database context.</param>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="fromSequence">The start sequence.</param>
    /// <param name="toSequence">The end sequence.</param>
    /// <param name="eventTypeFilter">Optional filter for event types.</param>
    /// <param name="eventPropertyFilter">Optional filter for event properties.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of event entities.</returns>
    /// <example>
    /// <code>
    /// var entities = await context.GetEventEntitiesBetweenSequences(streamId, fromSequence, toSequence);
    /// var filteredEntities = await context.GetEventEntitiesBetweenSequences(streamId, fromSequence, toSequence, new[] { typeof(SomeEvent) });
    /// </code>
    /// </example>
    public static async Task<List<EventEntity>> GetEventEntitiesBetweenSequences(this IDomainDbContext domainDbContext, IStreamId streamId, int fromSequence, int toSequence, Type[]? eventTypeFilter = null, IDictionary<string, string>? eventPropertyFilter = null, CancellationToken cancellationToken = default)
    {
        return await domainDbContext.Events.AsNoTracking()
            .Where(eventEntity =>
                eventEntity.StreamId == streamId.Id &&
                eventEntity.Sequence >= fromSequence &&
                eventEntity.Sequence <= toSequence)
            .ApplyFilters(eventTypeFilter, eventPropertyFilter)
            .OrderBy(eventEntity => eventEntity.Sequence)
            .ToListAsync(cancellationToken);
    }
}
