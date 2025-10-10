using Memoria.Messaging;

namespace Memoria.Examples.Messaging.RabbitMq.Messages;

public class TestTopicMessage : TopicMessage
{
    public string TestData { get; set; } = string.Empty;
}
