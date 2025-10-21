using System.Collections.Concurrent;
using System.Text.Json;
using Memoria.Messaging.Tests.Models.Messages;

namespace Memoria.Messaging.Tests;

public abstract class MockTestHelper
{
    private readonly ConcurrentBag<SentMessage> _sentMessages = [];
    private readonly ConcurrentDictionary<string, Exception> _sendFailures = new();

    private string? GetOriginalMessageType(string messageBody)
    {
        try
        {
            using var document = JsonDocument.Parse(messageBody);

            if (document.RootElement.TryGetProperty("QueueName", out _))
            {
                return typeof(TestQueueMessage).FullName;
            }

            if (document.RootElement.TryGetProperty("TopicName", out _))
            {
                return typeof(TestTopicMessage).FullName;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public void ClearSendFailure(string entityName)
    {
        _sendFailures.TryRemove(entityName, out _);
    }

    public void ClearAllSendFailures()
    {
        _sendFailures.Clear();
    }

    public IReadOnlyList<SentMessage> GetSentMessages()
    {
        return _sentMessages.ToList().AsReadOnly();
    }

    public IEnumerable<SentMessage> GetSentMessagesForEntity(string entityName)
    {
        return _sentMessages.Where(m => string.Equals(m.EntityName, entityName, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public IEnumerable<T> GetSentMessages<T>() where T : class
    {
        var targetTypeName = typeof(T).FullName;

        return _sentMessages
            .Where(m => m.ContentType == "application/json")
            .Where(m => m.OriginalMessageType == targetTypeName)
            .Select(m =>
            {
                try
                {
                    return JsonSerializer.Deserialize<T>(m.MessageBody);
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

    public IEnumerable<T> GetSentMessagesForEntity<T>(string entityName) where T : class
    {
        var targetTypeName = typeof(T).FullName;

        return _sentMessages
            .Where(m => string.Equals(m.EntityName, entityName, StringComparison.OrdinalIgnoreCase))
            .Where(m => m.ContentType == "application/json")
            .Where(m => m.OriginalMessageType == targetTypeName)
            .Select(m =>
            {
                try
                {
                    return JsonSerializer.Deserialize<T>(m.MessageBody);
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

    public void ClearSentMessages()
    {
        while (!_sentMessages.IsEmpty)
        {
            _sentMessages.TryTake(out _);
        }
    }

    public int GetMessageCountForEntity(string entityName)
    {
        return _sentMessages.Count(m => string.Equals(m.EntityName, entityName, StringComparison.OrdinalIgnoreCase));
    }

    public int TotalSentMessageCount => _sentMessages.Count;

    public void VerifyMessageSent(string entityName, int expectedCount = 1)
    {
        var actualCount = GetMessageCountForEntity(entityName);
        if (actualCount != expectedCount)
        {
            throw new InvalidOperationException(
                $"Expected {expectedCount} messages to {entityName}, but found {actualCount}");
        }
    }

    public void VerifySendMessageAsyncCalled(string entityName, int expectedTimes = 1)
    {
        var actualCount = GetMessageCountForEntity(entityName);
        if (actualCount != expectedTimes)
        {
            throw new InvalidOperationException(
                $"Expected BasicPublish to be called {expectedTimes} times for '{entityName}', but was called {actualCount} times");
        }
    }
}