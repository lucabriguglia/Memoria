using Memoria.Caching;
using Memoria.Commands;
using Memoria.Messaging;
using Memoria.Notifications;
using Memoria.Pipeline;
using Memoria.Queries;
using Memoria.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace Memoria.Extensions;

/// <summary>
/// Provides extension methods for IServiceCollection to add Memoria services.
/// </summary>
public static class ServiceCollectionExtensions
{
    private static readonly Type[] HandlerOpenGenerics =
    [
        typeof(ICommandHandler<>),
        typeof(ICommandHandler<,>),
        typeof(IQueryHandler<,>),
        typeof(INotificationHandler<>)
    ];

    /// <summary>
    /// Adds Memoria services to the dependency injection container.
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to.</param>
    /// <param name="types">The types whose assemblies are scanned for handlers.</param>
    public static void AddMemoria(this IServiceCollection services, params Type[] types)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IDispatcher, Dispatcher>();
        services.AddScoped<ICommandSender, CommandSender>();
        services.AddScoped<IQueryProcessor, QueryProcessor>();
        services.AddScoped<ICachingService, CachingService>();
        services.AddScoped<INotificationPublisher, NotificationPublisher>();
        services.AddScoped<IMessagePublisher, MessagePublisher>();
        services.AddScoped<IValidationService, ValidationService>();

        services.AddScoped<ICachingProvider, DefaultCachingProvider>();
        services.AddScoped<IMessagingProvider, DefaultMessagingProvider>();
        services.AddScoped<IValidationProvider, DefaultValidationProvider>();

        RegisterHandlers(services, types);
    }

    /// <summary>
    /// Registers a pipeline behavior. Behaviors run in registration order, with the first
    /// registered behavior wrapping the outermost layer. The behavior type must implement
    /// either <see cref="IPipelineBehavior{TRequest}"/> or <see cref="IPipelineBehavior{TRequest, TResponse}"/>.
    /// Open generic behavior types are supported.
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to.</param>
    /// <param name="behaviorType">The behavior implementation type. May be an open generic.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="services"/> or <paramref name="behaviorType"/> is null.</exception>
    /// <exception cref="ArgumentException">When <paramref name="behaviorType"/> does not implement an IPipelineBehavior interface.</exception>
    public static IServiceCollection AddMemoriaBehavior(this IServiceCollection services, Type behaviorType)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(behaviorType);

        var matched = behaviorType.GetInterfaces()
            .Where(i => i.IsGenericType && PipelineBehaviorOpenGenerics.Contains(i.GetGenericTypeDefinition()))
            .ToList();

        if (matched.Count == 0)
        {
            throw new ArgumentException(
                $"{behaviorType.Name} does not implement IPipelineBehavior<TRequest> or IPipelineBehavior<TRequest, TResponse>.",
                nameof(behaviorType));
        }

        if (behaviorType.IsGenericTypeDefinition)
        {
            foreach (var iface in matched)
            {
                services.AddScoped(iface.GetGenericTypeDefinition(), behaviorType);
            }
        }
        else
        {
            foreach (var iface in matched)
            {
                services.AddScoped(iface, behaviorType);
            }
        }

        return services;
    }

    /// <summary>
    /// Registers a pipeline behavior. Behaviors run in registration order, with the first
    /// registered behavior wrapping the outermost layer.
    /// </summary>
    /// <typeparam name="TBehavior">The behavior implementation type.</typeparam>
    /// <param name="services">The IServiceCollection to add services to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddMemoriaBehavior<TBehavior>(this IServiceCollection services)
        where TBehavior : class
    {
        return services.AddMemoriaBehavior(typeof(TBehavior));
    }

    private static readonly Type[] PipelineBehaviorOpenGenerics =
    [
        typeof(IPipelineBehavior<>),
        typeof(IPipelineBehavior<,>),
    ];

    private static void RegisterHandlers(IServiceCollection services, Type[] types)
    {
        var assemblies = types.Select(t => t.Assembly).Distinct();

        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (!type.IsClass || type.IsAbstract || type.IsGenericTypeDefinition)
                {
                    continue;
                }

                foreach (var interfaceType in type.GetInterfaces())
                {
                    if (!interfaceType.IsGenericType)
                    {
                        continue;
                    }

                    if (HandlerOpenGenerics.Contains(interfaceType.GetGenericTypeDefinition()))
                    {
                        services.AddScoped(interfaceType, type);
                    }
                }
            }
        }
    }
}
