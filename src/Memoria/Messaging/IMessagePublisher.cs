using OpenCqrs.Results;

namespace OpenCqrs.Messaging;

/// <summary>
/// Defines a contract for publishing messages within the messaging system.
/// </summary>
public interface IMessagePublisher
{
    /// <summary>
    /// Publishes a message to the messaging infrastructure.
    /// </summary>
    /// <typeparam name="TMessage">The type of the message to be published.</typeparam>
    /// <param name="message">The message instance to be published.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation, containing a <c>Result</c> which indicates success or failure.</returns>
    Task<Result> Publish<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : IMessage;
}
