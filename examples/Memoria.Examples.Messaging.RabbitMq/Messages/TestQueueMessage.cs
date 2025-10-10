using Memoria.Messaging;

namespace Memoria.Examples.Messaging.RabbitMq.Messages;

public class TestQueueMessage : QueueMessage
{
    public string TestData { get; set; } = string.Empty;
}
