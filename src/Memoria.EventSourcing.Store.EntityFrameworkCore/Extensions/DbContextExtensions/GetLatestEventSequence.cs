using Memoria.EventSourcing.Domain;
using Microsoft.EntityFrameworkCore;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

public static partial class IDomainDbContextExtensions
{
    /// <summary>
    /// Retrieves the latest event sequence number for a specified stream.
    /// </summary>
    /// <param name="domainDbContext">The domain database context.</param>
    /// <param name="streamId">The unique identifier for the event stream.</param>
    /// <param name="eventTypeFilter">An optional array of event types to filter the results.</param>
    /// <param name="eventPropertyFilter">An optional array of event properties to filter the results.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The latest sequence number in the stream.</returns>
    /// <example>
    /// <code>
    /// var sequence = await context.GetLatestEventSequence(streamId);
    /// </code>
    /// </example>
    public static async Task<int> GetLatestEventSequence(this IDomainDbContext domainDbContext, IStreamId streamId, Type[]? eventTypeFilter = null, IDictionary<string, string>? eventPropertyFilter = null, CancellationToken cancellationToken = default)
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
            .MaxAsync(eventEntity => (int?)eventEntity.Sequence, cancellationToken) ?? 0;
    }
}
