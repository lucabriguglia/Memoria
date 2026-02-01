using FluentAssertions;
using Memoria.Messaging.RabbitMq.InMemory.Tests.Models.Messages;
using Xunit;

namespace Memoria.Messaging.RabbitMq.InMemory.Tests.Features;

public class InMemoryRabbitMqMessagingProviderTests
{
    private readonly InMemoryRabbitMqStorage _storage = new();

    [Fact]
    public async Task SendQueueMessage_WithValidMessage_ShouldSucceed()
    {
        var provider = CreateProvider();
        var message = new TestQueueMessage
        {
            QueueName = "test-queue",
            TestData = "Test message data",
            Properties = new Dictionary<string, object> { { "key1", "value1" } }
        };

        var result = await provider.SendQueueMessage(message);

        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();

        var sentMessages = _storage.GetMessages();
        sentMessages.Should().HaveCount(1);

        var sentMessage = sentMessages[0];
        sentMessage.EntityName.Should().Be("test-queue");
        sentMessage.ContentType.Should().Be("application/json");
        sentMessage.MessageId.Should().NotBeNullOrEmpty();

        var deserializedMessages = _storage.GetMessages<TestQueueMessage>().ToArray();
        deserializedMessages.Should().HaveCount(1);

        var deserializedMessage = deserializedMessages.First();
        deserializedMessage.QueueName.Should().Be("test-queue");
        deserializedMessage.TestData.Should().Be("Test message data");
    }

    [Fact]
    public async Task SendTopicMessage_WithValidMessage_ShouldSucceed()
    {
        var provider = CreateProvider();
        var message = new TestTopicMessage
        {
            TopicName = "test-topic",
            TestData = "Test topic message",
            Properties = new Dictionary<string, object>
            {
                { "MessageType", "TestEvent" },
                { "Version", 1 }
            }
        };

        var result = await provider.SendTopicMessage(message);

        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();

        var topicMessages = _storage.GetMessagesForEntity("test-topic");
        topicMessages.Should().HaveCount(1);

        _storage.GetMessageCountForEntity("test-topic").Should().Be(1);
        _storage.TotalMessageCount.Should().Be(1);
    }

    [Fact]
    public async Task SendQueueMessage_WithEmptyQueueName_ShouldReturnFailure()
    {
        var provider = CreateProvider();
        var message = new TestQueueMessage
        {
            QueueName = string.Empty,
            TestData = "Test data"
        };

        var result = await provider.SendQueueMessage(message);

        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().NotBeNull();
        result.Failure!.Title.Should().Be("Queue name");
        result.Failure.Description.Should().Be("Queue name cannot be null or empty");

        _storage.TotalMessageCount.Should().Be(0);
    }

    [Fact]
    public async Task SendTopicMessage_WithEmptyTopicName_ShouldReturnFailure()
    {
        var provider = CreateProvider();
        var message = new TestTopicMessage
        {
            TopicName = string.Empty,
            TestData = "Test data"
        };

        var result = await provider.SendTopicMessage(message);

        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().NotBeNull();
        result.Failure!.Title.Should().Be("Topic name");
        result.Failure.Description.Should().Be("Topic name cannot be null or empty");
    }

    [Fact]
    public async Task SendQueueMessage_WithScheduledEnqueueTime_ShouldPreserveScheduledTime()
    {
        var provider = CreateProvider();
        var scheduledTime = DateTime.UtcNow.AddHours(1);
        var message = new TestQueueMessage
        {
            QueueName = "scheduled-queue",
            TestData = "Scheduled message",
            ScheduledEnqueueTimeUtc = scheduledTime
        };

        var result = await provider.SendQueueMessage(message);

        result.IsSuccess.Should().BeTrue();

        var sentMessages = _storage.GetMessages();
        sentMessages.Should().HaveCount(1);

        var sentMessage = sentMessages.First();
        sentMessage.ScheduledEnqueueTime.Should().NotBeNull();
        sentMessage.ScheduledEnqueueTime!.Value.Should().BeCloseTo(scheduledTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task SendQueueMessage_WithApplicationProperties_ShouldPreserveProperties()
    {
        var provider = CreateProvider();
        var message = new TestQueueMessage
        {
            QueueName = "properties-queue",
            TestData = "Message with properties",
            Properties = new Dictionary<string, object>
            {
                { "CustomProperty1", "Value1" },
                { "CustomProperty2", 42 },
                { "CustomProperty3", true }
            }
        };

        var result = await provider.SendQueueMessage(message);

        result.IsSuccess.Should().BeTrue();

        var sentMessages = _storage.GetMessages();
        var sentMessage = sentMessages.First();

        sentMessage.ApplicationProperties.Should().HaveCount(3);
        sentMessage.ApplicationProperties["CustomProperty1"].Should().Be("Value1");
        sentMessage.ApplicationProperties["CustomProperty2"].Should().Be(42);
        sentMessage.ApplicationProperties["CustomProperty3"].Should().Be(true);
    }

    [Fact]
    public async Task SendMultipleMessages_ShouldCaptureAllMessages()
    {
        var provider = CreateProvider();
        var queueMessage1 = new TestQueueMessage { QueueName = "queue1", TestData = "Queue message 1" };
        var queueMessage2 = new TestQueueMessage { QueueName = "queue2", TestData = "Queue message 2" };
        var topicMessage = new TestTopicMessage { TopicName = "topic1", TestData = "Topic message 1" };

        await provider.SendQueueMessage(queueMessage1);
        await provider.SendQueueMessage(queueMessage2);
        await provider.SendTopicMessage(topicMessage);

        _storage.TotalMessageCount.Should().Be(3);
        _storage.GetMessageCountForEntity("queue1").Should().Be(1);
        _storage.GetMessageCountForEntity("queue2").Should().Be(1);
        _storage.GetMessageCountForEntity("topic1").Should().Be(1);

        var allQueueMessages = _storage.GetMessages<TestQueueMessage>();
        allQueueMessages.Should().HaveCount(2);

        var allTopicMessages = _storage.GetMessages<TestTopicMessage>();
        allTopicMessages.Should().HaveCount(1);
    }

    [Fact]
    public async Task Clear_ShouldRemoveAllStoredMessages()
    {
        var provider = CreateProvider();
        var message = new TestQueueMessage { QueueName = "test-queue", TestData = "Test" };

        await provider.SendQueueMessage(message);
        _storage.TotalMessageCount.Should().Be(1);

        _storage.Clear();

        _storage.TotalMessageCount.Should().Be(0);
        _storage.GetMessages().Should().BeEmpty();
    }

    [Fact]
    public async Task GetMessagesByType_ShouldReturnOnlyMatchingType()
    {
        var provider = CreateProvider();
        var queueMessage = new TestQueueMessage { QueueName = "queue1", TestData = "Queue data" };
        var topicMessage = new TestTopicMessage { TopicName = "topic1", TestData = "Topic data" };

        await provider.SendQueueMessage(queueMessage);
        await provider.SendTopicMessage(topicMessage);

        _storage.TotalMessageCount.Should().Be(2);

        var queueMessages = _storage.GetMessages<TestQueueMessage>().ToArray();
        queueMessages.Should().HaveCount(1);
        queueMessages.First().TestData.Should().Be("Queue data");

        var topicMessages = _storage.GetMessages<TestTopicMessage>().ToArray();
        topicMessages.Should().HaveCount(1);
        topicMessages.First().TestData.Should().Be("Topic data");
    }

    [Fact]
    public async Task GetMessagesForEntity_ByType_ShouldReturnOnlyMatchingEntityAndType()
    {
        var provider = CreateProvider();
        var message1 = new TestQueueMessage { QueueName = "queue1", TestData = "Message 1" };
        var message2 = new TestQueueMessage { QueueName = "queue2", TestData = "Message 2" };

        await provider.SendQueueMessage(message1);
        await provider.SendQueueMessage(message2);

        var queue1Messages = _storage.GetMessagesForEntity<TestQueueMessage>("queue1").ToArray();
        queue1Messages.Should().HaveCount(1);
        queue1Messages.First().TestData.Should().Be("Message 1");

        var queue2Messages = _storage.GetMessagesForEntity<TestQueueMessage>("queue2").ToArray();
        queue2Messages.Should().HaveCount(1);
        queue2Messages.First().TestData.Should().Be("Message 2");
    }

    private InMemoryRabbitMqMessagingProvider CreateProvider()
    {
        return new InMemoryRabbitMqMessagingProvider(_storage);
    }
}
