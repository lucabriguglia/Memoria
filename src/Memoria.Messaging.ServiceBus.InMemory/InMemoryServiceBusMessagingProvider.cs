using System.Diagnostics;
using Memoria.Results;
using Newtonsoft.Json;

namespace Memoria.Messaging.ServiceBus.InMemory;

/// <summary>
/// Provides in-memory messaging functionality for testing and local development
/// without requiring an Azure Service Bus connection.
/// </summary>
public class InMemoryServiceBusMessagingProvider(InMemoryServiceBusStorage storage) : IMessagingProvider
{
    /// <summary>
    /// Sends a message to the specified queue (stored in-memory).
    /// </summary>
    /// <typeparam name="TMessage">The type of the message, which must implement IQueueMessage.</typeparam>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    public Task<Result> SendQueueMessage<TMessage>(TMessage message, CancellationToken cancellationToken = default) where TMessage : IQueueMessage
    {
        try
        {
            if (string.IsNullOrEmpty(message.QueueName))
            {
                return Task.FromResult<Result>(new Failure(Title: "Queue name", Description: "Queue name cannot be null or empty"));
            }

            var inMemoryMessage = CreateInMemoryMessage(message, message.QueueName);
            storage.AddMessage(inMemoryMessage);

            return Task.FromResult(Result.Ok());
        }
        catch (Exception ex)
        {
            var tagList = new TagList { { "Operation description", "Sending queue message" } };
            Activity.Current?.AddException(ex, tagList, DateTimeOffset.UtcNow);
            return Task.FromResult<Result>(new Failure
            (
                Title: "Error",
                Description: "There was an error when processing the request"
            ));
        }
    }

    /// <summary>
    /// Sends a message to the specified topic (stored in-memory).
    /// </summary>
    /// <typeparam name="TMessage">The type of the message, which must implement ITopicMessage.</typeparam>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    public Task<Result> SendTopicMessage<TMessage>(TMessage message, CancellationToken cancellationToken = default) where TMessage : ITopicMessage
    {
        try
        {
            if (string.IsNullOrEmpty(message.TopicName))
            {
                return Task.FromResult<Result>(new Failure(Title: "Topic name", Description: "Topic name cannot be null or empty"));
            }

            var inMemoryMessage = CreateInMemoryMessage(message, message.TopicName);
            storage.AddMessage(inMemoryMessage);

            return Task.FromResult(Result.Ok());
        }
        catch (Exception ex)
        {
            var tagList = new TagList { { "Operation description", "Sending topic message" } };
            Activity.Current?.AddException(ex, tagList, DateTimeOffset.UtcNow);
            return Task.FromResult<Result>(new Failure
            (
                Title: "Error",
                Description: "There was an error when processing the request"
            ));
        }
    }

    private static InMemoryMessage CreateInMemoryMessage<TMessage>(TMessage message, string entityName) where TMessage : IMessage
    {
        var json = JsonConvert.SerializeObject(message);

        var inMemoryMessage = new InMemoryMessage
        {
            EntityName = entityName,
            MessageBody = json,
            ContentType = "application/json",
            MessageId = Guid.NewGuid().ToString(),
            OriginalMessageType = message.GetType().FullName,
            SentAt = DateTimeOffset.UtcNow
        };

        if (message.ScheduledEnqueueTimeUtc.HasValue)
        {
            inMemoryMessage.ScheduledEnqueueTime = message.ScheduledEnqueueTimeUtc.Value;
        }

        foreach (var property in message.Properties)
        {
            inMemoryMessage.ApplicationProperties[property.Key] = property.Value;
        }

        return inMemoryMessage;
    }
}
