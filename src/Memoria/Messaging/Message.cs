using Newtonsoft.Json;

namespace Memoria.Messaging;

/// <summary>
/// Represents an abstract base class for messages in a messaging system.
/// This class provides common functionality and properties for derived message types.
/// </summary>
public abstract class Message : IMessage
{
    /// <summary>
    /// Gets or sets the scheduled enqueue time in UTC for the message in the messaging system.
    /// When specified, this property determines the time at which the message should become available for processing.
    /// A null value indicates that the message is not scheduled and should be enqueued immediately.
    /// </summary>
    [JsonIgnore]
    public DateTime? ScheduledEnqueueTimeUtc { get; set; }

    /// <summary>
    /// Gets or sets a collection of user-defined metadata associated with the message.
    /// This property allows for the inclusion of additional information about the message
    /// in the form of key-value pairs.
    /// </summary>
    [JsonIgnore]
    public IDictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
}
