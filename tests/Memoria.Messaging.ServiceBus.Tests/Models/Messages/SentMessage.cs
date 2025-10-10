using Azure.Messaging.ServiceBus;

namespace OpenCqrs.Messaging.ServiceBus.Tests.Models.Messages;

public class SentMessage
{
    public string EntityName { get; set; } = string.Empty;
    public ServiceBusMessage ServiceBusMessage { get; set; } = null!;
    public DateTimeOffset SentAt { get; set; }
    public string MessageBody { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public string? MessageId { get; set; }
    public DateTimeOffset? ScheduledEnqueueTime { get; set; }
    public Dictionary<string, object> ApplicationProperties { get; set; } = new();
    public string? OriginalMessageType { get; set; }
}
