using System.Text;
using Memoria.EventSourcing.Domain;
using Microsoft.Azure.Cosmos;

namespace Memoria.EventSourcing.Store.Cosmos.Extensions;

internal static class EventFilterQueryExtensions
{
    public static StringBuilder AppendEventFilters(this StringBuilder sql,
        Type[]? eventTypeFilter, IDictionary<string, string>? eventPropertyFilter)
    {
        if (eventTypeFilter is { Length: > 0 })
        {
            sql.Append(" AND ARRAY_CONTAINS(@eventTypes, c.eventType)");
        }

        if (eventPropertyFilter is { Count: > 0 })
        {
            for (var i = 0; i < eventPropertyFilter.Count; i++)
            {
                sql.Append($" AND CONTAINS(c.data, @propertyFilter{i})");
            }
        }

        return sql;
    }

    public static QueryDefinition BindEventFilterParameters(this QueryDefinition queryDefinition,
        Type[]? eventTypeFilter, IDictionary<string, string>? eventPropertyFilter)
    {
        if (eventTypeFilter is { Length: > 0 })
        {
            var eventTypes = eventTypeFilter
                .Select(eventType => TypeBindings.EventTypeBindings.FirstOrDefault(b => b.Value == eventType))
                .Select(b => b.Key).ToList();

            queryDefinition = queryDefinition.WithParameter("@eventTypes", eventTypes);
        }

        if (eventPropertyFilter is { Count: > 0 })
        {
            var index = 0;
            foreach (var filter in eventPropertyFilter)
            {
                queryDefinition = queryDefinition.WithParameter($"@propertyFilter{index}", $"\"{filter.Key}\":\"{filter.Value}\"");
                index++;
            }
        }

        return queryDefinition;
    }
}
