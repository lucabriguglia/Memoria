using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Memoria.Messaging.RabbitMq.Configuration;
using Memoria.Messaging.RabbitMq.Tests.Models.Messages;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using RabbitMQ.Client;

namespace Memoria.Messaging.RabbitMq.Tests;

public class MockRabbitMqTestHelper
{
    public IConnection MockConnection { get; }
    public IOptions<RabbitMqOptions> MockOptions { get; }

    private readonly ConcurrentDictionary<string, IModel> _mockChannels = new();
    private readonly ConcurrentBag<SentMessage> _sentMessages = [];
    private readonly Dictionary<string, Exception> _publishFailures = new();

    public MockRabbitMqTestHelper()
    {
        MockConnection = Substitute.For<IConnection>();
        MockOptions = CreateMockOptions();
        SetupDefaultBehavior();
    }

    private static IOptions<RabbitMqOptions> CreateMockOptions()
    {
        var options = new RabbitMqOptions
        {
            ConnectionString = "amqp://localhost",
            VirtualHost = "/",
            RequestedConnectionTimeout = 60000,
            RequestedHeartbeat = 60,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            DefaultExchangeName = "amq.topic",
            DelayedExchangeName = "delayed_exchange",
            CreateDelayedExchange = false
        };

        var mockOptions = Substitute.For<IOptions<RabbitMqOptions>>();
        mockOptions.Value.Returns(options);
        return mockOptions;
    }

    private void SetupDefaultBehavior()
    {
        MockConnection.CreateModel().Returns(_ => CreateMockChannel());
        MockConnection.IsOpen.Returns(true);
    }

    private IModel CreateMockChannel()
    {
        var channel = Substitute.For<IModel>();
        channel.IsOpen.Returns(true);

        channel.CreateBasicProperties().Returns(_ =>
        {
            var props = Substitute.For<IBasicProperties>();

            string? contentType = null;
            string? messageId = null;
            var persistent = false;
            IDictionary<string, object>? headers = null;

            props.When(p => p.ContentType = Arg.Any<string>()).Do(callInfo => contentType = callInfo.Arg<string>());
            props.ContentType.Returns(_ => contentType);

            props.When(p => p.MessageId = Arg.Any<string>()).Do(callInfo => messageId = callInfo.Arg<string>());
            props.MessageId.Returns(_ => messageId);

            props.When(p => p.Persistent = Arg.Any<bool>()).Do(callInfo => persistent = callInfo.Arg<bool>());
            props.Persistent.Returns(_ => persistent);

            props.When(p => p.Headers = Arg.Any<IDictionary<string, object>>()).Do(callInfo => headers = callInfo.Arg<IDictionary<string, object>>());
            props.Headers.Returns(_ => headers);

            return props;
        });

        channel.When(x => x.BasicPublish(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IBasicProperties>(),
            Arg.Any<ReadOnlyMemory<byte>>()))
            .Do(call =>
            {
                var exchange = call.ArgAt<string>(0);
                var routingKey = call.ArgAt<string>(1);
                var properties = call.Arg<IBasicProperties>();
                var body = call.Arg<ReadOnlyMemory<byte>>();

                var entityName = string.IsNullOrEmpty(exchange) ? routingKey : exchange;

                if (_publishFailures.TryGetValue(entityName, out var exception))
                {
                    throw exception;
                }

                var messageBody = Encoding.UTF8.GetString(body.Span);

                _sentMessages.Add(new SentMessage
                {
                    EntityName = entityName,
                    Exchange = exchange,
                    RoutingKey = routingKey,
                    SentAt = DateTimeOffset.UtcNow,
                    MessageBody = messageBody,
                    ContentType = properties.ContentType,
                    MessageId = properties.MessageId,
                    Headers = properties.Headers?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, object>(),
                    OriginalMessageType = GetOriginalMessageType(messageBody),
                    Persistent = properties.Persistent,
                    Body = body,
                    BasicProperties = properties,
                    ScheduledEnqueueTime = ExtractScheduledTime(properties)
                });
            });

        channel.When(x => x.QueueDeclare(
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<IDictionary<string, object>>()))
            .Do(_ => { });

        channel.When(x => x.ExchangeDeclare(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<IDictionary<string, object>>()))
            .Do(_ => { });

        return channel;
    }

    private static string? GetOriginalMessageType(string messageBody)
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

    private static DateTime? ExtractScheduledTime(IBasicProperties properties)
    {
        if (properties.Headers != null && properties.Headers.TryGetValue("x-delay", out var delayObj))
        {
            if (delayObj is int delay and > 0)
            {
                return DateTime.UtcNow.AddMilliseconds(delay);
            }
        }

        return null;
    }

    public void SetupPublishFailure(string entityName, string errorMessage = "Mock RabbitMQ error")
    {
        var exception = new InvalidOperationException(errorMessage);
        _publishFailures[entityName] = exception;
    }

    public void SetupCreateChannelFailure(string errorMessage = "Mock create channel error")
    {
        MockConnection.CreateModel().Throws(new InvalidOperationException(errorMessage));
    }

    public void ClearPublishFailure(string entityName)
    {
        _publishFailures.Remove(entityName);
    }

    public void ClearAllPublishFailures()
    {
        _publishFailures.Clear();
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

    public void VerifyBasicPublishCalled(string entityName, int expectedTimes = 1)
    {
        var actualCount = GetMessageCountForEntity(entityName);
        if (actualCount != expectedTimes)
        {
            throw new InvalidOperationException($"Expected BasicPublish to be called {expectedTimes} times for '{entityName}', but was called {actualCount} times");
        }
    }

    public void VerifyCreateChannelCalled(int expectedTimes = 1)
    {
        MockConnection.Received(expectedTimes).CreateModel();
    }
}
