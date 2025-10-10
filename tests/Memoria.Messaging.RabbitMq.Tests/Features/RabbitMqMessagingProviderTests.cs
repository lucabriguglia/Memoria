using FluentAssertions;
using Memoria.Messaging.RabbitMq.Tests.Models.Messages;
using Xunit;

namespace Memoria.Messaging.RabbitMq.Tests.Features;

public class RabbitMqMessagingProviderTests
{
    private readonly MockRabbitMqTestHelper _mockHelper = new();

    [Fact]
    public async Task SendQueueMessage_WithValidMessage_ShouldSucceed()
    {
        var provider = CreateRabbitMqMessagingProvider();
        var message = new TestQueueMessage
        {
            QueueName = "test-queue",
            TestData = "Test message data",
            Properties = new Dictionary<string, object> { { "key1", "value1" } }
        };

        var result = await provider.SendQueueMessage(message);

        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();

        var sentMessages = _mockHelper.GetSentMessages();
        sentMessages.Should().HaveCount(1);

        var sentMessage = sentMessages[0];
        sentMessage.EntityName.Should().Be("test-queue");
        sentMessage.ContentType.Should().Be("application/json");
        sentMessage.MessageId.Should().NotBeNullOrEmpty();
        sentMessage.Exchange.Should().Be(string.Empty);
        sentMessage.RoutingKey.Should().Be("test-queue");

        var deserializedMessages = _mockHelper.GetSentMessages<TestQueueMessage>().ToArray();
        deserializedMessages.Should().HaveCount(1);

        var deserializedMessage = deserializedMessages.First();
        deserializedMessage.QueueName.Should().Be("test-queue");
        deserializedMessage.TestData.Should().Be("Test message data");
    }

    [Fact]
    public async Task SendTopicMessage_WithValidMessage_ShouldSucceed()
    {
        var provider = CreateRabbitMqMessagingProvider();
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

        var topicMessages = _mockHelper.GetSentMessagesForEntity("test-topic").ToArray();
        topicMessages.Should().HaveCount(1);

        var sentMessage = topicMessages.First();
        sentMessage.Exchange.Should().Be("test-topic");
        sentMessage.RoutingKey.Should().Be("message");

        _mockHelper.GetMessageCountForEntity("test-topic").Should().Be(1);
        _mockHelper.TotalSentMessageCount.Should().Be(1);
    }

    [Fact]
    public async Task SendQueueMessage_WithEmptyQueueName_ShouldReturnFailure()
    {
        var provider = CreateRabbitMqMessagingProvider();
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

        _mockHelper.TotalSentMessageCount.Should().Be(0);
    }

    [Fact]
    public async Task SendTopicMessage_WithEmptyTopicName_ShouldReturnFailure()
    {
        var provider = CreateRabbitMqMessagingProvider();
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
    public async Task SendQueueMessage_WithScheduledEnqueueTime_ShouldSetDelayHeader()
    {
        var provider = CreateRabbitMqMessagingProvider();
        var scheduledTime = DateTime.UtcNow.AddHours(1);
        var message = new TestQueueMessage
        {
            QueueName = "scheduled-queue",
            TestData = "Scheduled message",
            ScheduledEnqueueTimeUtc = scheduledTime
        };

        var result = await provider.SendQueueMessage(message);

        result.IsSuccess.Should().BeTrue();

        var sentMessages = _mockHelper.GetSentMessages();
        sentMessages.Should().HaveCount(1);

        var sentMessage = sentMessages.First();
        sentMessage.Headers.Should().ContainKey("x-delay");
        sentMessage.Headers["x-delay"].Should().BeOfType<int>();

        var delay = (int)sentMessage.Headers["x-delay"];
        delay.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SendQueueMessage_WithProperties_ShouldIncludeHeaders()
    {
        var provider = CreateRabbitMqMessagingProvider();
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

        var sentMessages = _mockHelper.GetSentMessages();
        var sentMessage = sentMessages.First();

        sentMessage.Headers.Should().ContainKey("CustomProperty1");
        sentMessage.Headers.Should().ContainKey("CustomProperty2");
        sentMessage.Headers.Should().ContainKey("CustomProperty3");
        sentMessage.Headers["CustomProperty1"].Should().Be("Value1");
        sentMessage.Headers["CustomProperty2"].Should().Be(42);
        sentMessage.Headers["CustomProperty3"].Should().Be(true);
    }

    [Fact]
    public async Task SendTopicMessage_WithRoutingKey_ShouldUseCustomRoutingKey()
    {
        var provider = CreateRabbitMqMessagingProvider();
        var message = new TestTopicMessage
        {
            TopicName = "custom-topic",
            TestData = "Message with custom routing key",
            Properties = new Dictionary<string, object>
            {
                { "RoutingKey", "custom.routing.key" }
            }
        };

        var result = await provider.SendTopicMessage(message);

        result.IsSuccess.Should().BeTrue();

        var sentMessages = _mockHelper.GetSentMessages();
        var sentMessage = sentMessages.First();

        sentMessage.Exchange.Should().Be("custom-topic");
        sentMessage.RoutingKey.Should().Be("custom.routing.key");
    }

    [Fact]
    public async Task SendQueueMessage_WhenPublishThrowsException_ShouldReturnFailure()
    {
        var provider = CreateRabbitMqMessagingProvider();
        var message = new TestQueueMessage
        {
            QueueName = "error-queue",
            TestData = "This will fail"
        };

        _mockHelper.SetupPublishFailure("error-queue");

        var result = await provider.SendQueueMessage(message);

        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().NotBeNull();
        result.Failure!.Title.Should().Be("Error");
        result.Failure.Description.Should().Be("There was an error when processing the request");
    }

    [Fact]
    public async Task SendMultipleMessages_ShouldCaptureAllMessages()
    {
        var provider = CreateRabbitMqMessagingProvider();
        var queueMessage1 = new TestQueueMessage { QueueName = "queue1", TestData = "Queue message 1" };
        var queueMessage2 = new TestQueueMessage { QueueName = "queue2", TestData = "Queue message 2" };
        var topicMessage = new TestTopicMessage { TopicName = "topic1", TestData = "Topic message 1" };

        await provider.SendQueueMessage(queueMessage1);
        await provider.SendQueueMessage(queueMessage2);
        await provider.SendTopicMessage(topicMessage);

        _mockHelper.TotalSentMessageCount.Should().Be(3);
        _mockHelper.GetMessageCountForEntity("queue1").Should().Be(1);
        _mockHelper.GetMessageCountForEntity("queue2").Should().Be(1);
        _mockHelper.GetMessageCountForEntity("topic1").Should().Be(1);

        var allQueueMessages = _mockHelper.GetSentMessages<TestQueueMessage>();
        allQueueMessages.Should().HaveCount(2);

        var allTopicMessages = _mockHelper.GetSentMessages<TestTopicMessage>();
        allTopicMessages.Should().HaveCount(1);
    }

    [Fact]
    public async Task ClearSentMessages_ShouldRemoveAllCapturedMessages()
    {
        var provider = CreateRabbitMqMessagingProvider();
        var message = new TestQueueMessage { QueueName = "test-queue", TestData = "Test" };

        await provider.SendQueueMessage(message);
        _mockHelper.TotalSentMessageCount.Should().Be(1);

        _mockHelper.ClearSentMessages();

        _mockHelper.TotalSentMessageCount.Should().Be(0);
        _mockHelper.GetSentMessages().Should().BeEmpty();
    }

    [Fact]
    public async Task VerifyMessageSent_WithCorrectCount_ShouldNotThrow()
    {
        var provider = CreateRabbitMqMessagingProvider();
        var message = new TestQueueMessage { QueueName = "verify-queue", TestData = "Verify test" };

        await provider.SendQueueMessage(message);

        _mockHelper.VerifyMessageSent("verify-queue");
    }

    [Fact]
    public async Task VerifyMessageSent_WithIncorrectCount_ShouldThrow()
    {
        var provider = CreateRabbitMqMessagingProvider();
        var message = new TestQueueMessage { QueueName = "verify-queue", TestData = "Verify test" };

        await provider.SendQueueMessage(message);

        var action = () => _mockHelper.VerifyMessageSent("verify-queue", 2);
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Expected 2 messages to verify-queue, but found 1");
    }

    private RabbitMqMessagingProvider CreateRabbitMqMessagingProvider()
    {
        return new RabbitMqMessagingProvider(_mockHelper.MockOptions, _mockHelper.MockConnection);
    }
}
