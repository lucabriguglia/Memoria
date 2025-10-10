using OpenCqrs.Results;

namespace OpenCqrs.Messaging;

/// <summary>
/// Defines an interface for messaging providers to handle the sending of messages to queues and topics.
/// </summary>
public interface IMessagingProvider
{
    /// <summary>
    /// Sends a message to a specified queue using the implemented messaging provider.
    /// </summary>
    /// <typeparam name="TMessage">The type of the message to be sent. It must implement the <see cref="IQueueMessage"/> interface.</typeparam>
    /// <param name="message">The message to send to the queue.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Task{Result}"/> representing the result of the operation.</returns>
    Task<Result> SendQueueMessage<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : IQueueMessage;

    /// <summary>
    /// Sends a message to a specified topic using the implemented messaging provider.
    /// </summary>
    /// <typeparam name="TMessage">The type of the message to be sent. It must implement the <see cref="ITopicMessage"/> interface.</typeparam>
    /// <param name="message">The message to send to the topic.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Task{Result}"/> representing the result of the operation.</returns>
    Task<Result> SendTopicMessage<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : ITopicMessage;
}
