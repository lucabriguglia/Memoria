using System.Reflection;
using Memoria.EventSourcing.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Memoria.EventSourcing.Dcb.Extensions;

/// <summary>
/// Registration for the dynamic consistency boundary model.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Scans the supplied types' assemblies for DCB aggregates, DCB projections and domain events,
    /// binds them, and registers <see cref="IDcbDomainService"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="types">One type per assembly to scan.</param>
    /// <remarks>
    /// Safe to call alongside <c>AddMemoriaEventSourcing</c>, in either order. Events bind into the
    /// map both models share, so a binding key claimed by two different CLR types throws; aggregates
    /// and projections bind into <see cref="DcbTypeBindings"/>, so the same domain name may be used
    /// by both models.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddMemoriaDcb(typeof(Program));
    /// services.AddMemoriaDcbEntityFrameworkCore&lt;MyDbContext&gt;();
    /// </code>
    /// </example>
    public static void AddMemoriaDcb(this IServiceCollection services, params Type[] types)
    {
        var eventTypeBindings = new Dictionary<string, Type>();
        var aggregateTypeBindings = new Dictionary<string, Type>();
        var projectionTypeBindings = new Dictionary<string, Type>();

        foreach (var type in types)
        {
            var assembly = type.Assembly;

            Bind<IEvent, EventType>(assembly, eventTypeBindings,
                attribute => TypeBindings.GetTypeBindingKey(attribute.Name, attribute.Version));

            Bind<IDcbAggregateRoot, AggregateType>(assembly, aggregateTypeBindings,
                attribute => TypeBindings.GetTypeBindingKey(attribute.Name, attribute.Version));

            Bind<IDcbProjection, ProjectionType>(assembly, projectionTypeBindings,
                attribute => TypeBindings.GetTypeBindingKey(attribute.Name, attribute.Version));
        }

        // Events are shared with the streamed model; aggregates and projections are not. All three
        // merge rather than assign, because another registration may already have run.
        TypeBindings.EventTypeBindings =
            TypeBindings.Merge(TypeBindings.EventTypeBindings, eventTypeBindings, "Event");
        DcbTypeBindings.AggregateTypeBindings =
            TypeBindings.Merge(DcbTypeBindings.AggregateTypeBindings, aggregateTypeBindings, "DCB aggregate");
        DcbTypeBindings.ProjectionTypeBindings =
            TypeBindings.Merge(DcbTypeBindings.ProjectionTypeBindings, projectionTypeBindings, "DCB projection");

        services.AddScoped<IDcbDomainService, DefaultDcbDomainService>();
    }

    private static void Bind<TModel, TAttribute>(
        Assembly assembly, Dictionary<string, Type> bindings, Func<TAttribute, string> keyOf)
        where TAttribute : Attribute
    {
        var candidates = assembly.GetTypes()
            .Where(candidate => candidate.GetTypeInfo().IsClass
                                && !candidate.GetTypeInfo().IsAbstract
                                && typeof(TModel).IsAssignableFrom(candidate));

        foreach (var candidate in candidates)
        {
            var attribute = candidate.GetCustomAttribute<TAttribute>();
            if (attribute is null)
            {
                continue;
            }

            bindings[keyOf(attribute)] = candidate;
        }
    }
}
