using System.Collections.Concurrent;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Memoria.Messaging.ServiceBus.Tests.Models.Messages;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Memoria.Messaging.ServiceBus.Tests;

public class MockServiceBusTestHelper
{
    public ServiceBusClient MockServiceBusClient { get; }

    private readonly ConcurrentDictionary<string, ServiceBusSender> _mockQueueSenders = new();
    private readonly ConcurrentDictionary<string, ServiceBusSender> _mockTopicSenders = new();
    private readonly ConcurrentBag<SentMessage> _sentMessages = [];
    private readonly ConcurrentDictionary<string, Exception> _sendFailures = new();

    public MockServiceBusTestHelper()
    {
        MockServiceBusClient = Substitute.For<ServiceBusClient>();
        SetupDefaultBehavior();
    }

    private void SetupDefaultBehavior()
    {
        MockServiceBusClient.CreateSender(Arg.Any<string>())
            .Returns(call =>
            {
                var entityName = call.Arg<string>();
                var sender = CreateMockSender(entityName);

                _mockQueueSenders.TryAdd(entityName, sender);
                _mockTopicSenders.TryAdd(entityName, sender);

                return sender;
            });
    }

    private ServiceBusSender CreateMockSender(string entityName)
    {
        var sender = Substitute.For<ServiceBusSender>();

        sender.SendMessageAsync(Arg.Any<ServiceBusMessage>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                if (_sendFailures.TryGetValue(entityName, out var exception))
                {
                    throw exception;
                }

                var message = call.Arg<ServiceBusMessage>();

                _sentMessages.Add(new SentMessage
                {
                    EntityName = entityName,
                    ServiceBusMessage = message,
                    SentAt = DateTimeOffset.UtcNow,
                    MessageBody = message.Body.ToString(),
                    ContentType = message.ContentType,
                    MessageId = message.MessageId,
                    ScheduledEnqueueTime = message.ScheduledEnqueueTime,
                    ApplicationProperties = new Dictionary<string, object>(message.ApplicationProperties),
                    OriginalMessageType = GetOriginalMessageType(message.Body.ToString())
                });

                return Task.CompletedTask;
            });

        sender.SendMessagesAsync(Arg.Any<IEnumerable<ServiceBusMessage>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                if (_sendFailures.TryGetValue(entityName, out var exception))
                {
                    throw exception;
                }

                var messages = call.Arg<IEnumerable<ServiceBusMessage>>();

                foreach (var message in messages)
                {
                    _sentMessages.Add(new SentMessage
                    {
                        EntityName = entityName,
                        ServiceBusMessage = message,
                        SentAt = DateTimeOffset.UtcNow,
                        MessageBody = message.Body.ToString(),
                        ContentType = message.ContentType,
                        MessageId = message.MessageId,
                        ScheduledEnqueueTime = message.ScheduledEnqueueTime,
                        ApplicationProperties = new Dictionary<string, object>(message.ApplicationProperties),
                        OriginalMessageType = GetOriginalMessageType(message.Body.ToString())
                    });
                }

                return Task.CompletedTask;
            });

        sender.DisposeAsync().Returns(ValueTask.CompletedTask);

        return sender;
    }

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

    public void SetupSendFailure(string entityName, string errorMessage = "Mock service bus error")
    {
        var exception = new ServiceBusException(errorMessage, ServiceBusFailureReason.ServiceTimeout);
        _sendFailures[entityName] = exception;
    }

    public void SetupCreateSenderFailure(string entityName, string errorMessage = "Mock create sender error")
    {
        MockServiceBusClient.CreateSender(entityName)
            .Throws(new ServiceBusException(errorMessage, ServiceBusFailureReason.GeneralError));
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
        return _sentMessages.Where(m => string.Equals(m.EntityName, entityName, StringComparison.OrdinalIgnoreCase)).ToList();
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
            throw new InvalidOperationException($"Expected {expectedCount} messages to {entityName}, but found {actualCount}");
        }
    }

    public void VerifySendMessageAsyncCalled(string entityName, int expectedTimes = 1)
    {
        if (!_mockQueueSenders.ContainsKey(entityName) && !_mockTopicSenders.ContainsKey(entityName))
        {
            throw new InvalidOperationException($"No sender was created for entity '{entityName}'");
        }
    }

    public void VerifyCreateSenderCalled(string entityName, int expectedTimes = 1)
    {
        MockServiceBusClient.Received(expectedTimes).CreateSender(entityName);
    }
}
