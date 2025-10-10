using Memoria.Results;

namespace Memoria.Messaging;

/// <summary>
/// Provides functionality to publish messages to a messaging provider.
/// </summary>
/// <remarks>
/// The <see cref="MessagePublisher"/> class is responsible for sending messages
/// either to a queue or a topic based on the message type. It works with a messaging
/// provider implementing the <see cref="IMessagingProvider"/> interface to handle
/// the actual delivery of the messages.
/// </remarks>
public class MessagePublisher(IMessagingProvider messagingProvider) : IMessagePublisher
{
    /// <summary>
    /// Publishes a message using the appropriate messaging mechanism based on the message type.
    /// </summary>
    /// <typeparam name="TMessage">The type of the message to be published. Must implement either IQueueMessage or ITopicMessage interface.</typeparam>
    /// <param name="message">The message to be published.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Result"/> indicating the success or failure of the operation.</returns>
    /// <exception cref="Exception">Thrown when the message implements both IQueueMessage and ITopicMessage interfaces.</exception>
    /// <exception cref="NotSupportedException">Thrown when the message does not implement either IQueueMessage or ITopicMessage interface.</exception>
    public async Task<Result> Publish<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : IMessage
    {
        if (message is IQueueMessage && message is ITopicMessage)
        {
            throw new InvalidOperationException("The message cannot implement both the IQueueMessage and the ITopicMessage interfaces");
        }

        if (message is IQueueMessage queueMessage)
        {
            return await messagingProvider.SendQueueMessage(queueMessage, cancellationToken);
        }

        if (message is ITopicMessage topicMessage)
        {
            return await messagingProvider.SendTopicMessage(topicMessage, cancellationToken);
        }

        throw new NotSupportedException("The message must implement either the IQueueMessage or the ITopicMessage interface");
    }
}
