using Memoria.Messaging;
using OpenCqrs.Messaging;

namespace OpenCqrs.Examples.Messaging.RabbitMq.Messages;

public class TestQueueMessage : QueueMessage
{
    public string TestData { get; set; } = string.Empty;
}
