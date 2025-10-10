using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Memoria.Messaging;
using Memoria.Results;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using OpenCqrs.Messaging.RabbitMq.Configuration;
using RabbitMQ.Client;

namespace OpenCqrs.Messaging.RabbitMq;

/// <summary>
/// Provides messaging functionality using RabbitMQ for sending queue and topic messages.
/// </summary>
public class RabbitMqMessagingProvider : IMessagingProvider, IAsyncDisposable, IDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly IConnection _connection;
    private readonly ConcurrentDictionary<string, IModel> _queueChannels = new();
    private readonly ConcurrentDictionary<string, IModel> _exchangeChannels = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RabbitMqMessagingProvider"/> class.
    /// </summary>
    /// <param name="options">The RabbitMQ options.</param>
    public RabbitMqMessagingProvider(IOptions<RabbitMqOptions> options)
    {
        _options = options.Value;
        _connection = CreateConnection();

        if (_options.CreateDelayedExchange)
        {
            EnsureDelayedExchangeExists();
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RabbitMqMessagingProvider"/> class with a custom connection.
    /// </summary>
    /// <param name="options">The RabbitMQ options.</param>
    /// <param name="connection">The RabbitMQ connection.</param>
    public RabbitMqMessagingProvider(IOptions<RabbitMqOptions> options, IConnection connection)
    {
        _options = options.Value;
        _connection = connection;

        if (_options.CreateDelayedExchange)
        {
            EnsureDelayedExchangeExists();
        }
    }

    /// <summary>
    /// Sends a message to the specified queue.
    /// </summary>
    /// <typeparam name="TMessage">The type of the message, which must implement IQueueMessage.</typeparam>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the result of the send operation.</returns>
    public Task<Result> SendQueueMessage<TMessage>(TMessage message, CancellationToken cancellationToken = default) where TMessage : IQueueMessage
    {
        try
        {
            if (string.IsNullOrEmpty(message.QueueName))
            {
                return Task.FromResult<Result>(new Failure(Title: "Queue name", Description: "Queue name cannot be null or empty"));
            }

            var channel = GetOrCreateQueueChannel(message.QueueName);

            channel.QueueDeclare(
                queue: message.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var body = CreateMessageBody(message);
            var properties = CreateBasicProperties(channel, message);

            if (message.ScheduledEnqueueTimeUtc.HasValue)
            {
                var delay = message.ScheduledEnqueueTimeUtc.Value - DateTimeOffset.UtcNow;
                if (delay.TotalMilliseconds > 0)
                {
                    properties.Headers ??= new Dictionary<string, object>();
                    properties.Headers["x-delay"] = (int)delay.TotalMilliseconds;

                    channel.BasicPublish(
                        exchange: _options.DelayedExchangeName,
                        routingKey: message.QueueName,
                        basicProperties: properties,
                        body: body);
                }
                else
                {
                    channel.BasicPublish(
                        exchange: string.Empty,
                        routingKey: message.QueueName,
                        basicProperties: properties,
                        body: body);
                }
            }
            else
            {
                channel.BasicPublish(
                    exchange: string.Empty,
                    routingKey: message.QueueName,
                    basicProperties: properties,
                    body: body);
            }

            return Task.FromResult(Result.Ok());
        }
        catch (Exception ex)
        {
            var tagList = new TagList { { "Operation description", "Sending RabbitMQ queue message" } };
            Activity.Current?.AddException(ex, tagList, DateTimeOffset.UtcNow);
            return Task.FromResult<Result>(new Failure
            (
                Title: "Error",
                Description: "There was an error when processing the request"
            ));
        }
    }

    /// <summary>
    /// Sends a message to the specified topic.
    /// </summary>
    /// <typeparam name="TMessage">The type of the message, which must implement ITopicMessage.</typeparam>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the result of the send operation.</returns>
    public Task<Result> SendTopicMessage<TMessage>(TMessage message, CancellationToken cancellationToken = default) where TMessage : ITopicMessage
    {
        try
        {
            if (string.IsNullOrEmpty(message.TopicName))
            {
                return Task.FromResult<Result>(new Failure(Title: "Topic name", Description: "Topic name cannot be null or empty"));
            }

            var channel = GetOrCreateExchangeChannel(message.TopicName);

            channel.ExchangeDeclare(
                exchange: message.TopicName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                arguments: null);

            var body = CreateMessageBody(message);
            var properties = CreateBasicProperties(channel, message);

            var routingKey = GetRoutingKey(message);

            if (message.ScheduledEnqueueTimeUtc.HasValue)
            {
                var delay = message.ScheduledEnqueueTimeUtc.Value - DateTimeOffset.UtcNow;
                if (delay.TotalMilliseconds > 0)
                {
                    properties.Headers ??= new Dictionary<string, object>();
                    properties.Headers["x-delay"] = (int)delay.TotalMilliseconds;
                    properties.Headers["x-original-exchange"] = message.TopicName;
                    properties.Headers["x-original-routing-key"] = routingKey;

                    channel.BasicPublish(
                        exchange: _options.DelayedExchangeName,
                        routingKey: $"topic.{message.TopicName}.{routingKey}",
                        basicProperties: properties,
                        body: body);
                }
                else
                {
                    channel.BasicPublish(
                        exchange: message.TopicName,
                        routingKey: routingKey,
                        basicProperties: properties,
                        body: body);
                }
            }
            else
            {
                channel.BasicPublish(
                    exchange: message.TopicName,
                    routingKey: routingKey,
                    basicProperties: properties,
                    body: body);
            }

            return Task.FromResult(Result.Ok());
        }
        catch (Exception ex)
        {
            var tagList = new TagList { { "Operation description", "Sending RabbitMQ topic message" } };
            Activity.Current?.AddException(ex, tagList, DateTimeOffset.UtcNow);
            return Task.FromResult<Result>(new Failure
            (
                Title: "Error",
                Description: "There was an error when processing the request"
            ));
        }
    }

    private IConnection CreateConnection()
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(_options.ConnectionString),
            VirtualHost = _options.VirtualHost,
            RequestedConnectionTimeout = TimeSpan.FromMilliseconds(_options.RequestedConnectionTimeout),
            RequestedHeartbeat = TimeSpan.FromSeconds(_options.RequestedHeartbeat),
            AutomaticRecoveryEnabled = _options.AutomaticRecoveryEnabled,
            TopologyRecoveryEnabled = _options.TopologyRecoveryEnabled
        };

        return factory.CreateConnection();
    }

    private void EnsureDelayedExchangeExists()
    {
        using var channel = _connection.CreateModel();
        try
        {
            var arguments = new Dictionary<string, object>
            {
                { "x-delayed-type", "direct" }
            };

            channel.ExchangeDeclare(
                exchange: _options.DelayedExchangeName,
                type: "x-delayed-message",
                durable: true,
                autoDelete: false,
                arguments: arguments);
        }
        catch (Exception)
        {
            // If the delayed message plugin is not available, we'll handle scheduling differently
            // or fall back to immediate delivery
        }
    }

    private IModel GetOrCreateQueueChannel(string queueName)
    {
        if (_queueChannels.TryGetValue(queueName, out var existingChannel) && existingChannel.IsOpen)
        {
            return existingChannel;
        }

        var newChannel = _connection.CreateModel();
        _queueChannels[queueName] = newChannel;
        return newChannel;
    }

    private IModel GetOrCreateExchangeChannel(string exchangeName)
    {
        if (_exchangeChannels.TryGetValue(exchangeName, out var existingChannel) && existingChannel.IsOpen)
        {
            return existingChannel;
        }

        var newChannel = _connection.CreateModel();
        _exchangeChannels[exchangeName] = newChannel;
        return newChannel;
    }

    private static byte[] CreateMessageBody<TMessage>(TMessage message) where TMessage : IMessage
    {
        var json = JsonConvert.SerializeObject(message);
        return Encoding.UTF8.GetBytes(json);
    }

    private static IBasicProperties CreateBasicProperties<TMessage>(IModel channel, TMessage message) where TMessage : IMessage
    {
        var properties = channel.CreateBasicProperties();
        properties.ContentType = "application/json";
        properties.ContentEncoding = "utf-8";
        properties.MessageId = Guid.NewGuid().ToString();
        properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        properties.Persistent = true;

        if (message.Properties.Count > 0)
        {
            properties.Headers = new Dictionary<string, object>();
            foreach (var property in message.Properties)
            {
                properties.Headers[property.Key] = property.Value;
            }
        }

        properties.Headers ??= new Dictionary<string, object>();
        properties.Headers.Add("AssemblyQualifiedName", message.GetType().AssemblyQualifiedName);

        return properties;
    }

    private static string GetRoutingKey<TMessage>(TMessage message) where TMessage : ITopicMessage
    {
        if (message.Properties.TryGetValue("RoutingKey", out var routingKeyObj) && routingKeyObj is string routingKey)
        {
            return routingKey;
        }

        return "message";
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            foreach (var channel in _queueChannels.Values)
            {
                if (channel.IsOpen)
                {
                    channel.Close();
                }

                channel.Dispose();
            }

            foreach (var channel in _exchangeChannels.Values)
            {
                if (channel.IsOpen)
                {
                    channel.Close();
                }

                channel.Dispose();
            }

            if (_connection.IsOpen)
            {
                _connection.Close();
            }

            _connection.Dispose();

            _queueChannels.Clear();
            _exchangeChannels.Clear();
        }
        catch (Exception ex)
        {
            var tagList = new TagList { { "Operation description", "Disposing RabbitMQ messaging provider" } };
            Activity.Current?.AddException(ex, tagList, DateTimeOffset.UtcNow);
        }
        finally
        {
            _disposed = true;
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
