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
    /// Retrieves event entities up to a specified date for a given stream.
    /// </summary>
    /// <param name="domainDbContext">The domain database context.</param>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="upToDate">The end date.</param>
    /// <param name="eventTypeFilter">Optional filter for event types.</param>
    /// <param name="eventPropertyFilter">Optional filter for event properties.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of event entities.</returns>
    /// <example>
    /// <code>
    /// var entities = await context.GetEventEntitiesUpToDate(streamId, upToDate);
    /// var filteredEntities = await context.GetEventEntitiesUpToDate(streamId, upToDate, new[] { typeof(SomeEvent) });
    /// </code>
    /// </example>
    public static async Task<List<EventEntity>> GetEventEntitiesUpToDate(this IDomainDbContext domainDbContext, IStreamId streamId, DateTimeOffset upToDate, Type[]? eventTypeFilter = null, IDictionary<string, string>? eventPropertyFilter = null, CancellationToken cancellationToken = default)
    {
        return await domainDbContext.Events.AsNoTracking()
            .Where(eventEntity =>
                eventEntity.StreamId == streamId.Id &&
                eventEntity.CreatedDate <= upToDate)
            .ApplyFilters(eventTypeFilter, eventPropertyFilter)
            .OrderBy(eventEntity => eventEntity.Sequence)
            .ToListAsync(cancellationToken);
    }
}
