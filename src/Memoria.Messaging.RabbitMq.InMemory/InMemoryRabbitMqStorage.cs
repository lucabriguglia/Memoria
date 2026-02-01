using System.Collections.Concurrent;
using Newtonsoft.Json;

namespace Memoria.Messaging.RabbitMq.InMemory;

/// <summary>
/// Shared in-memory storage for RabbitMQ messaging implementation.
/// This class provides thread-safe storage for sent messages, allowing test verification.
/// </summary>
public class InMemoryRabbitMqStorage
{
    private readonly ConcurrentBag<InMemoryMessage> _messages = [];

    /// <summary>
    /// Gets all stored messages.
    /// </summary>
    public IReadOnlyList<InMemoryMessage> GetMessages()
    {
        return _messages.ToList().AsReadOnly();
    }

    /// <summary>
    /// Gets all stored messages for a specific entity (queue or topic name).
    /// </summary>
    public IEnumerable<InMemoryMessage> GetMessagesForEntity(string entityName)
    {
        return _messages.Where(m => string.Equals(m.EntityName, entityName, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    /// <summary>
    /// Gets all stored messages that can be deserialized to the specified type.
    /// </summary>
    public IEnumerable<T> GetMessages<T>() where T : class
    {
        var targetTypeName = typeof(T).FullName;

        return _messages
            .Where(m => m.ContentType == "application/json")
            .Where(m => m.OriginalMessageType == targetTypeName)
            .Select(m =>
            {
                try
                {
                    return JsonConvert.DeserializeObject<T>(m.MessageBody);
                }
                catch
                {
                    return null;
                }
            })
            .Where(m => m != null)
            .Cast<T>()
            .ToList();
    }

    /// <summary>
    /// Gets all stored messages for a specific entity that can be deserialized to the specified type.
    /// </summary>
    public IEnumerable<T> GetMessagesForEntity<T>(string entityName) where T : class
    {
        var targetTypeName = typeof(T).FullName;

        return _messages
            .Where(m => string.Equals(m.EntityName, entityName, StringComparison.OrdinalIgnoreCase))
            .Where(m => m.ContentType == "application/json")
            .Where(m => m.OriginalMessageType == targetTypeName)
            .Select(m =>
            {
                try
                {
                    return JsonConvert.DeserializeObject<T>(m.MessageBody);
                }
                catch
                {
                    return null;
                }
            })
            .Where(m => m != null)
            .Cast<T>()
            .ToList();
    }

    /// <summary>
    /// Gets the number of messages sent to a specific entity.
    /// </summary>
    public int GetMessageCountForEntity(string entityName)
    {
        return _messages.Count(m => string.Equals(m.EntityName, entityName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets the total number of stored messages.
    /// </summary>
    public int TotalMessageCount => _messages.Count;

    /// <summary>
    /// Adds a message to the storage.
    /// </summary>
    internal void AddMessage(InMemoryMessage message)
    {
        _messages.Add(message);
    }

    /// <summary>
    /// Clears all stored data. Useful for test cleanup.
    /// </summary>
    public void Clear()
    {
        while (!_messages.IsEmpty)
        {
            _messages.TryTake(out _);
        }
    }
}

/// <summary>
/// Represents a message captured by the in-memory RabbitMQ provider.
/// </summary>
public class InMemoryMessage
{
    public string EntityName { get; set; } = string.Empty;
    public string MessageBody { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public string? MessageId { get; set; }
    public DateTime? ScheduledEnqueueTime { get; set; }
    public Dictionary<string, object> ApplicationProperties { get; set; } = new();
    public string? OriginalMessageType { get; set; }
    public DateTimeOffset SentAt { get; set; }
}
