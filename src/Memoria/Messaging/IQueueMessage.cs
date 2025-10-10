namespace Memoria.Messaging;

/// <summary>
/// Represents a queue message within a messaging system.
/// This interface defines the required properties that must be implemented by any queue message.
/// </summary>
public interface IQueueMessage : IMessage
{
    /// <summary>
    /// Gets or sets the name of the queue in the messaging system.
    /// This property is used to identify the specific queue where the message
    /// should be sent or from where it is received.
    /// </summary>
    string QueueName { get; set; }
}
