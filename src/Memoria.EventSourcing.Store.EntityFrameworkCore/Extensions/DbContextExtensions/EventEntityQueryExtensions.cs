using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

internal static class EventEntityQueryExtensions
{
    public static IQueryable<EventEntity> ApplyFilters(this IQueryable<EventEntity> query,
        Type[]? eventTypeFilter, IDictionary<string, string>? eventPropertyFilter)
    {
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

        return query;
    }
}
