using RabbitMQ.Client;

namespace Memoria.Messaging.RabbitMq.Tests.Models.Messages;

public class SentMessage
{
    public string EntityName { get; set; } = string.Empty;
    public string Exchange { get; set; } = string.Empty;
    public string RoutingKey { get; set; } = string.Empty;
    public DateTimeOffset SentAt { get; set; }
    public string MessageBody { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public string? MessageId { get; set; }
    public DateTime? ScheduledEnqueueTime { get; set; }
    public Dictionary<string, object> Headers { get; set; } = new();
    public string? OriginalMessageType { get; set; }
    public bool Persistent { get; set; }
    public ReadOnlyMemory<byte> Body { get; set; }
    public IBasicProperties BasicProperties { get; set; } = null!;
}
