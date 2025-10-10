using Newtonsoft.Json;

namespace OpenCqrs.Messaging;

/// <summary>
/// Represents an abstract base class for topic-based messages in a messaging system.
/// This class inherits from the <see cref="Message"/> class and implements the <see cref="ITopicMessage"/> interface.
/// It defines a required property for specifying the name of the topic the message is associated with.
/// </summary>
public abstract class TopicMessage : Message, ITopicMessage
{
    /// <summary>
    /// Gets or sets the name of the topic associated with the message.
    /// </summary>
    [JsonIgnore]
    public required string TopicName { get; set; }
}
