using Azure.Messaging.ServiceBus;
using Memoria.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenCqrs.Messaging.ServiceBus.Configuration;

namespace OpenCqrs.Messaging.ServiceBus.Extensions;

/// <summary>
/// Provides extension methods for configuring OpenCQRS with Azure Service Bus messaging.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds OpenCQRS Service Bus messaging provider to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add the provider to.</param>
    /// <param name="options">The Service Bus configuration options.</param>
    public static void AddOpenCqrsServiceBus(this IServiceCollection services, ServiceBusOptions options)
    {
        var serviceBusClient = new ServiceBusClient(options.ConnectionString);
        services.Replace(ServiceDescriptor.Scoped<IMessagingProvider>(_ => new ServiceBusMessagingProvider(serviceBusClient)));
    }
}
