using Azure.Messaging.ServiceBus;
using Memoria.Messaging.ServiceBus.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Memoria.Messaging.ServiceBus.Extensions;

/// <summary>
/// Provides extension methods for configuring Memoria with Azure Service Bus messaging.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Memoria Service Bus messaging provider to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add the provider to.</param>
    /// <param name="options">The Service Bus configuration options.</param>
    public static void AddMemoriaServiceBus(this IServiceCollection services, ServiceBusOptions options)
    {
        var serviceBusClient = new ServiceBusClient(options.ConnectionString);
        services.Replace(ServiceDescriptor.Scoped<IMessagingProvider>(_ => new MessagingProvider(serviceBusClient)));
    }
}