namespace Memoria.Messaging.RabbitMq.Configuration;

/// <summary>
/// Configuration options for RabbitMQ messaging.
/// </summary>
public class RabbitMqOptions
{
    /// <summary>
    /// Gets or sets the connection string for RabbitMQ.
    /// </summary>
    public required string ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the virtual host for RabbitMQ.
    /// </summary>
    public string VirtualHost { get; set; } = "/";

    /// <summary>
    /// Gets or sets the requested connection timeout in milliseconds.
    /// </summary>
    public int RequestedConnectionTimeout { get; set; } = 60000;

    /// <summary>
    /// Gets or sets the requested heartbeat interval in seconds.
    /// </summary>
    public int RequestedHeartbeat { get; set; } = 60;

    /// <summary>
    /// Gets or sets a value indicating whether automatic recovery is enabled.
    /// </summary>
    public bool AutomaticRecoveryEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether topology recovery is enabled.
    /// </summary>
    public bool TopologyRecoveryEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the default exchange name.
    /// </summary>
    public string DefaultExchangeName { get; set; } = "amq.topic";

    /// <summary>
    /// Gets or sets the delayed exchange name.
    /// </summary>
    public string DelayedExchangeName { get; set; } = "delayed_exchange";

    /// <summary>
    /// Gets or sets a value indicating whether to create the delayed exchange.
    /// </summary>
    public bool CreateDelayedExchange { get; set; } = true;
}
