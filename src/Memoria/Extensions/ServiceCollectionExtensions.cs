using Memoria.Caching;
using Memoria.Commands;
using Memoria.Messaging;
using Memoria.Notifications;
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
