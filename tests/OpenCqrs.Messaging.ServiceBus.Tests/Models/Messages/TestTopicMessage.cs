using Memoria.Messaging;

namespace OpenCqrs.Messaging.ServiceBus.Tests.Models.Messages;

public class TestTopicMessage : ITopicMessage
{
    public string TopicName { get; set; } = string.Empty;
    public DateTime? ScheduledEnqueueTimeUtc { get; set; }
    public IDictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
    public string TestData { get; set; } = string.Empty;
}
