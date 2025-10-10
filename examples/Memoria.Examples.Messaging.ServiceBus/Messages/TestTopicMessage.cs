using Memoria.Messaging;

namespace OpenCqrs.Examples.Messaging.ServiceBus.Messages;

public class TestTopicMessage : TopicMessage
{
    public string TestData { get; set; } = string.Empty;
}
