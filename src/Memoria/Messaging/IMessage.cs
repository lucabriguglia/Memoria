namespace Memoria.Messaging;

/// <summary>
/// Defines the properties and behavior for a basic message in a messaging system.
/// This interface is designed to represent a message with optional scheduling
/// and additional metadata properties.
/// </summary>
public interface IMessage
{
    DateTime? ScheduledEnqueueTimeUtc { get; set; }
    IDictionary<string, object> Properties { get; set; }
}
