using System.Reflection;
using Memoria.EventSourcing.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Memoria.EventSourcing.Extensions;

/// <summary>
/// Extension methods for IServiceCollection to configure event sourcing.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Configures event sourcing by scanning assemblies for domain events and aggregates.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="types">The types to scan.</param>
    /// <exception cref="ArgumentNullException">Thrown when services or types are null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when duplicate type-binding keys are encountered.</exception>
    public static void AddOpenCqrsEventSourcing(this IServiceCollection services, params Type[] types)
    {
        var eventTypeBindings = new Dictionary<string, Type>();
        var aggregateTypeBindings = new Dictionary<string, Type>();

        foreach (var type in types)
        {
            var assembly = type.Assembly;

            var events = assembly.GetTypes()
                .Where(t => t.GetTypeInfo().IsClass && !t.GetTypeInfo().IsAbstract && typeof(IEvent).IsAssignableFrom(t))
                .ToList();

            var aggregates = assembly.GetTypes()
                .Where(t => t.GetTypeInfo().IsClass && !t.GetTypeInfo().IsAbstract && typeof(IAggregateRoot).IsAssignableFrom(t))
                .ToList();

            foreach (var @event in events)
            {
                var eventType = @event.GetCustomAttribute<EventType>();
                if (eventType is null)
                {
                    continue;
                }
                eventTypeBindings.Add(TypeBindings.GetTypeBindingKey(eventType.Name, eventType.Version), @event);
            }

            foreach (var aggregate in aggregates)
            {
                var aggregateType = aggregate.GetCustomAttribute<AggregateType>();
                if (aggregateType is null)
                {
                    continue;
                }
                aggregateTypeBindings.Add(TypeBindings.GetTypeBindingKey(aggregateType.Name, aggregateType.Version), aggregate);
            }
        }

        TypeBindings.EventTypeBindings = eventTypeBindings;
        TypeBindings.AggregateTypeBindings = aggregateTypeBindings;

        services.AddScoped<IDomainService, DefaultDomainService>();
    }
}
