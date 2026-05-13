using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Filtering;
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
    /// <param name="dataFilter">Optional strategy used to apply <paramref name="eventPropertyFilter"/>. Defaults to a substring match on the JSON data column.</param>
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
        IEventDataFilter? dataFilter = null,
        CancellationToken cancellationToken = default)
    {
        return await domainDbContext.Events.AsNoTracking()
            .Where(eventEntity => eventEntity.StreamId == streamId.Id)
            .ApplyFilters(eventTypeFilter, eventPropertyFilter, dataFilter)
            .OrderBy(eventEntity => eventEntity.Sequence)
            .ToListAsync(cancellationToken);
    }
}