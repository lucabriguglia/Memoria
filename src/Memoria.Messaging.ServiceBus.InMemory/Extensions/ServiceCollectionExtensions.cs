using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Memoria.Messaging.ServiceBus.InMemory.Extensions;

/// <summary>
/// Provides extension methods for configuring Memoria with the in-memory Service Bus messaging provider.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the in-memory Service Bus messaging provider to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add the provider to.</param>
    public static void AddMemoriaInMemoryServiceBus(this IServiceCollection services)
    {
        var storage = new InMemoryServiceBusStorage();
        services.AddSingleton(storage);
        services.Replace(ServiceDescriptor.Singleton<IMessagingProvider>(_ => new InMemoryServiceBusMessagingProvider(storage)));
    }

    /// <summary>
    /// Adds the in-memory Service Bus messaging provider to the service collection with a shared storage instance.
    /// </summary>
    /// <param name="services">The service collection to add the provider to.</param>
    /// <param name="storage">The shared storage instance for test verification.</param>
    public static void AddMemoriaInMemoryServiceBus(this IServiceCollection services, InMemoryServiceBusStorage storage)
    {
        services.AddSingleton(storage);
        services.Replace(ServiceDescriptor.Singleton<IMessagingProvider>(_ => new InMemoryServiceBusMessagingProvider(storage)));
    }
}
