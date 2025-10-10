using Memoria.Messaging.RabbitMq.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Memoria.Messaging.RabbitMq.Extensions;

/// <summary>
/// Provides extension methods for configuring RabbitMQ messaging services in the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds RabbitMQ messaging provider to the service collection with custom options.
    /// </summary>
    public static IServiceCollection AddMemoriaRabbitMq(this IServiceCollection services, Action<RabbitMqOptions> options)
    {
        services.Configure(options);
        services.AddSingleton<IMessagingProvider, RabbitMqMessagingProvider>();

        return services;
    }

    /// <summary>
    /// Adds RabbitMQ messaging provider to the service collection with a connection string.
    /// </summary>
    public static IServiceCollection AddMemoriaRabbitMq(this IServiceCollection services, string connectionString)
    {
        services.Configure<RabbitMqOptions>(options =>
        {
            options.ConnectionString = connectionString;
        });

        services.Replace(ServiceDescriptor.Scoped<IMessagingProvider>(provider => new RabbitMqMessagingProvider(provider.GetRequiredService<IOptions<RabbitMqOptions>>())));

        return services;
    }
}
