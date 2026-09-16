using System.Text;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Filtering;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;

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

    /// <summary>
    /// Binds the filter's values as parameters, turning each CLR type in the event type filter into
    /// its stored key through the process-wide bindings.
    /// </summary>
    public static QueryDefinition BindEventFilterParameters(this QueryDefinition queryDefinition,
        Type[]? eventTypeFilter, IDictionary<string, string>? eventPropertyFilter) =>
        queryDefinition.BindEventFilterParameters(eventTypeFilter, eventPropertyFilter, TypeBindingSet.Default);

    /// <summary>
    /// Binds the filter's values as parameters, turning each CLR type in the event type filter into
    /// its stored key through the given bindings.
    /// </summary>
    /// <param name="queryDefinition">The query to bind on.</param>
    /// <param name="eventTypeFilter">The CLR types to keep, or null or empty to keep every event.</param>
    /// <param name="eventPropertyFilter">The property values to match, or null to match every event.</param>
    /// <param name="bindings">The set a CLR type is turned into its stored key through.</param>
    public static QueryDefinition BindEventFilterParameters(this QueryDefinition queryDefinition,
        Type[]? eventTypeFilter, IDictionary<string, string>? eventPropertyFilter, TypeBindingSet bindings)
    {
        if (eventTypeFilter is { Length: > 0 })
        {
            var bindingKeysByType = bindings.GetEventBindingKeysByType();
            var eventTypes = eventTypeFilter
                .Select(bindingKeysByType.GetValueOrDefault)
                .ToList();

            queryDefinition = queryDefinition.WithParameter("@eventTypes", eventTypes);
        }

        if (eventPropertyFilter is { Count: > 0 })
        {
            var index = 0;
            foreach (var filter in eventPropertyFilter)
            {
                var needle = $"{JsonConvert.ToString(filter.Key)}:{EventPropertyFilterValue.ToJsonLiteral(filter.Value)}";
                queryDefinition = queryDefinition.WithParameter($"@propertyFilter{index}", needle);
                index++;
            }
        }

        return queryDefinition;
    }
}
