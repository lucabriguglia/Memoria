using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Filtering;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

internal static class EventEntityQueryExtensions
{
    private static readonly IEventDataFilter DefaultDataFilter = new SubstringEventDataFilter();

    public static IQueryable<EventEntity> ApplyFilters(this IQueryable<EventEntity> query,
        Type[]? eventTypeFilter, IDictionary<string, string>? eventPropertyFilter,
        IEventDataFilter? dataFilter = null)
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
            var filter = dataFilter ?? DefaultDataFilter;
            foreach (var property in eventPropertyFilter)
            {
                query = filter.ApplyPropertyFilter(query, property.Key, property.Value);
            }
        }

        return query;
    }
}
