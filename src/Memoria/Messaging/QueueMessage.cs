using Newtonsoft.Json;

namespace OpenCqrs.Messaging;

/// <summary>
/// Represents a base class for queue messages in a messaging system.
/// This class provides functionality- and properties-specific to messages
/// that are sent to and received from a queue.
/// </summary>
public abstract class QueueMessage : Message, IQueueMessage
{
    /// Gets or sets the name of the queue to which the message will be sent or from which it will be received.
    /// This property is required and is used to specify the queue's unique identifier in the messaging infrastructure.
    [JsonIgnore]
    public required string QueueName { get; set; }
}
