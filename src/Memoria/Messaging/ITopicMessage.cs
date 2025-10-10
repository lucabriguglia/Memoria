namespace OpenCqrs.Messaging;

/// <summary>
/// Represents a message associated with a specific topic in a messaging system.
/// Implementers of this interface define the name of the target topic as well as other message details.
/// </summary>
public interface ITopicMessage : IMessage
{
    /// <summary>
    /// Gets or sets the name of the topic associated with the message.
    /// This property specifies the target topic in a messaging system.
    /// The topic name is used to route the message to the appropriate channel or exchange.
    /// </summary>
    string TopicName { get; set; }
}
