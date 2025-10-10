using Memoria.Caching;
using Memoria.Commands;
using Memoria.Messaging;
using Memoria.Notifications;
using Memoria.Queries;
using Memoria.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace Memoria.Extensions;

/// <summary>
/// Provides extension methods for IServiceCollection to add OpenCQRS services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds OpenCQRS services to the dependency injection container.
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to.</param>
    /// <param name="types">The types to scan for handlers.</param>
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

        var typeList = types.ToList();

        services.Scan(s => s
            .FromAssembliesOf(typeList)
            .AddClasses(classes => classes.AssignableTo(typeof(ICommandHandler<>)))
                .AsImplementedInterfaces()
                .WithScopedLifetime()
            .AddClasses(classes => classes.AssignableTo(typeof(ICommandHandler<,>)))
                .AsImplementedInterfaces()
                .WithScopedLifetime()
            .AddClasses(classes => classes.AssignableTo(typeof(IQueryHandler<,>)))
            .AsImplementedInterfaces()
            .WithScopedLifetime()
            .AddClasses(classes => classes.AssignableTo(typeof(INotificationHandler<>)))
                .AsImplementedInterfaces()
                .WithScopedLifetime());
    }
}
