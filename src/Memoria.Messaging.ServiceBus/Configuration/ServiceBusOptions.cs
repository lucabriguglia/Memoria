namespace OpenCqrs.Messaging.ServiceBus.Configuration;

/// <summary>
/// Represents the configuration options for Service Bus messaging.
/// </summary>
public class ServiceBusOptions
{
    /// <summary>
    /// Gets or sets the connection string for the Service Bus.
    /// </summary>
    public required string ConnectionString { get; set; }
}
