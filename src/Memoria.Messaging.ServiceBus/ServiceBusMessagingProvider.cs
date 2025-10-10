using System.Collections.Concurrent;
using System.Diagnostics;
using Azure.Messaging.ServiceBus;
using Memoria.Results;
using Newtonsoft.Json;

namespace Memoria.Messaging.ServiceBus;

/// <summary>
/// Provides messaging functionality using Azure Service Bus for sending queue and topic messages.
/// </summary>
public class ServiceBusMessagingProvider(ServiceBusClient serviceBusClient) : IMessagingProvider, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, ServiceBusSender> _queueSenders = new();
    private readonly ConcurrentDictionary<string, ServiceBusSender> _topicSenders = new();

    /// <summary>
    /// Sends a message to the specified queue.
    /// </summary>
    /// <typeparam name="TMessage">The type of the message, which must implement IQueueMessage.</typeparam>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    public async Task<Result> SendQueueMessage<TMessage>(TMessage message, CancellationToken cancellationToken = default) where TMessage : IQueueMessage
    {
        try
        {
            if (string.IsNullOrEmpty(message.QueueName))
            {
                return new Failure(Title: "Queue name", Description: "Queue name cannot be null or empty");
            }

            var sender = GetQueueSender(message.QueueName);
            var serviceBusMessage = CreateServiceBusMessage(message);

            await sender.SendMessageAsync(serviceBusMessage, cancellationToken);

            return Result.Ok();
        }
        catch (Exception ex)
        {
            var tagList = new TagList { { "Operation description", "Sending queue message" } };
            Activity.Current?.AddException(ex, tagList, DateTimeOffset.UtcNow);
            return new Failure
            (
                Title: "Error",
                Description: "There was an error when processing the request"
            );
        }
    }

    /// <summary>
    /// Sends a message to the specified topic.
    /// </summary>
    /// <typeparam name="TMessage">The type of the message, which must implement ITopicMessage.</typeparam>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    public async Task<Result> SendTopicMessage<TMessage>(TMessage message, CancellationToken cancellationToken = default) where TMessage : ITopicMessage
    {
        try
        {
            if (string.IsNullOrEmpty(message.TopicName))
            {
                return new Failure(Title: "Topic name", Description: "Topic name cannot be null or empty");
            }

            var sender = GetTopicSender(message.TopicName);
            var serviceBusMessage = CreateServiceBusMessage(message);

            await sender.SendMessageAsync(serviceBusMessage, cancellationToken);

            return Result.Ok();
        }
        catch (Exception ex)
        {
            var tagList = new TagList { { "Operation description", "Sending topic message" } };
            Activity.Current?.AddException(ex, tagList, DateTimeOffset.UtcNow);
            return new Failure
            (
                Title: "Error",
                Description: "There was an error when processing the request"
            );
        }
    }

    private ServiceBusSender GetQueueSender(string queueName)
    {
        if (_queueSenders.TryGetValue(queueName, out var queueSender))
        {
            return queueSender;
        }

        queueSender = serviceBusClient.CreateSender(queueName);
        _queueSenders[queueName] = queueSender;

        return queueSender;
    }

    private ServiceBusSender GetTopicSender(string topicName)
    {
        if (_topicSenders.TryGetValue(topicName, out var topicSender))
        {
            return topicSender;
        }

        topicSender = serviceBusClient.CreateSender(topicName);
        _topicSenders[topicName] = topicSender;

        return topicSender;
    }

    private ServiceBusMessage CreateServiceBusMessage<TMessage>(TMessage message) where TMessage : IMessage
    {
        var json = JsonConvert.SerializeObject(message);
        var serviceBusMessage = new ServiceBusMessage(json)
        {
            ContentType = "application/json",
            MessageId = Guid.NewGuid().ToString()
        };

        if (message.ScheduledEnqueueTimeUtc.HasValue)
        {
            serviceBusMessage.ScheduledEnqueueTime = message.ScheduledEnqueueTimeUtc.Value;
        }

        foreach (var property in message.Properties)
        {
            serviceBusMessage.ApplicationProperties[property.Key] = property.Value;
        }

        return serviceBusMessage;
    }

    /// <summary>
    /// Disposes of all cached senders and the ServiceBusClient asynchronously.
    /// </summary>
    /// <returns>A ValueTask representing the asynchronous dispose operation.</returns>
    public async ValueTask DisposeAsync()
    {
        foreach (var sender in _queueSenders.Values)
        {
            await sender.DisposeAsync();
        }

        foreach (var sender in _topicSenders.Values)
        {
            await sender.DisposeAsync();
        }

        await serviceBusClient.DisposeAsync();

        _queueSenders.Clear();
        _topicSenders.Clear();
    }
}
