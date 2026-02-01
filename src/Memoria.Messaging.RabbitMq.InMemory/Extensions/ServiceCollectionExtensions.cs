using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Memoria.Messaging.RabbitMq.InMemory.Extensions;

/// <summary>
/// Provides extension methods for configuring Memoria with the in-memory RabbitMQ messaging provider.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the in-memory RabbitMQ messaging provider to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add the provider to.</param>
    public static void AddMemoriaInMemoryRabbitMq(this IServiceCollection services)
    {
        var storage = new InMemoryRabbitMqStorage();
        services.AddSingleton(storage);
        services.Replace(ServiceDescriptor.Singleton<IMessagingProvider>(_ => new InMemoryRabbitMqMessagingProvider(storage)));
    }

    /// <summary>
    /// Adds the in-memory RabbitMQ messaging provider to the service collection with a shared storage instance.
    /// </summary>
    /// <param name="services">The service collection to add the provider to.</param>
    /// <param name="storage">The shared storage instance for test verification.</param>
    public static void AddMemoriaInMemoryRabbitMq(this IServiceCollection services, InMemoryRabbitMqStorage storage)
    {
        services.AddSingleton(storage);
        services.Replace(ServiceDescriptor.Singleton<IMessagingProvider>(_ => new InMemoryRabbitMqMessagingProvider(storage)));
    }
}
