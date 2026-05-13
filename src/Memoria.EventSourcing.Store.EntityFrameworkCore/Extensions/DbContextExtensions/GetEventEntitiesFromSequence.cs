using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Filtering;
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
    /// <param name="dataFilter">Optional strategy used to apply <paramref name="eventPropertyFilter"/>.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A list of event entities ordered by sequence.</returns>
    public static async Task<List<EventEntity>> GetEventEntitiesFromSequence(this IDomainDbContext domainDbContext,
        IStreamId streamId, int fromSequence, Type[]? eventTypeFilter = null, IDictionary<string, string>? eventPropertyFilter = null,
        IEventDataFilter? dataFilter = null,
        CancellationToken cancellationToken = default)
    {
        return await domainDbContext.Events.AsNoTracking()
            .Where(eventEntity =>
                eventEntity.StreamId == streamId.Id &&
                eventEntity.Sequence >= fromSequence)
            .ApplyFilters(eventTypeFilter, eventPropertyFilter, dataFilter)
            .OrderBy(eventEntity => eventEntity.Sequence)
            .ToListAsync(cancellationToken);
    }
}