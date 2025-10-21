using FluentAssertions;
using Memoria.Messaging.Tests.Models.Messages;
using Xunit;

namespace Memoria.Messaging.Tests.Features;

public abstract class MessagingProviderTests(IMessagingProviderFactory messagingProviderFactory)
    : TestBase(messagingProviderFactory)
{
    [Fact]
    public async Task SendQueueMessage_WithValidMessage_ShouldSucceed()
    {
        var message = new TestQueueMessage
        {
            QueueName = "test-queue",
            TestData = "Test message data",
            Properties = new Dictionary<string, object> { { "key1", "value1" } }
        };

        var result = await MessagingProvider.SendQueueMessage(message);

        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();

        var sentMessages = MockServiceBusTestHelper.GetSentMessages();
        sentMessages.Should().HaveCount(1);

        var sentMessage = sentMessages[0];
        sentMessage.EntityName.Should().Be("test-queue");
        sentMessage.ContentType.Should().Be("application/json");
        sentMessage.MessageId.Should().NotBeNullOrEmpty();

        var deserializedMessages = MockServiceBusTestHelper.GetSentMessages<TestQueueMessage>().ToArray();
        deserializedMessages.Should().HaveCount(1);

        var deserializedMessage = deserializedMessages.First();
        deserializedMessage.QueueName.Should().Be("test-queue");
        deserializedMessage.TestData.Should().Be("Test message data");
    }

    [Fact]
    public async Task SendTopicMessage_WithValidMessage_ShouldSucceed()
    {
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

        var result = await MessagingProvider.SendTopicMessage(message);

        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();

        var topicMessages = MockServiceBusTestHelper.GetSentMessagesForEntity("test-topic");
        topicMessages.Should().HaveCount(1);

        MockServiceBusTestHelper.GetMessageCountForEntity("test-topic").Should().Be(1);
        MockServiceBusTestHelper.TotalSentMessageCount.Should().Be(1);
    }

    [Fact]
    public async Task SendQueueMessage_WithEmptyQueueName_ShouldReturnFailure()
    {
        var message = new TestQueueMessage
        {
            QueueName = string.Empty,
            TestData = "Test data"
        };

        var result = await MessagingProvider.SendQueueMessage(message);

        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().NotBeNull();
        result.Failure!.Title.Should().Be("Queue name");
        result.Failure.Description.Should().Be("Queue name cannot be null or empty");

        MockServiceBusTestHelper.TotalSentMessageCount.Should().Be(0);
    }

    [Fact]
    public async Task SendTopicMessage_WithEmptyTopicName_ShouldReturnFailure()
    {
        var message = new TestTopicMessage
        {
            TopicName = string.Empty,
            TestData = "Test data"
        };

        var result = await MessagingProvider.SendTopicMessage(message);

        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().NotBeNull();
        result.Failure!.Title.Should().Be("Topic name");
        result.Failure.Description.Should().Be("Topic name cannot be null or empty");
    }

    [Fact]
    public async Task SendQueueMessage_WithScheduledEnqueueTime_ShouldSetScheduledTime()
    {
        var scheduledTime = DateTime.UtcNow.AddHours(1);
        var message = new TestQueueMessage
        {
            QueueName = "scheduled-queue",
            TestData = "Scheduled message",
            ScheduledEnqueueTimeUtc = scheduledTime
        };

        var result = await MessagingProvider.SendQueueMessage(message);

        result.IsSuccess.Should().BeTrue();

        var sentMessages = MockServiceBusTestHelper.GetSentMessages();
        sentMessages.Should().HaveCount(1);

        var sentMessage = sentMessages.First();
        sentMessage.ScheduledEnqueueTime.Should().NotBeNull();
        sentMessage.ScheduledEnqueueTime!.Value.DateTime.Should().BeCloseTo(scheduledTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task SendQueueMessage_WithApplicationProperties_ShouldIncludeProperties()
    {
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

        var result = await MessagingProvider.SendQueueMessage(message);

        result.IsSuccess.Should().BeTrue();

        var sentMessages = MockServiceBusTestHelper.GetSentMessages();
        var sentMessage = sentMessages.First();

        sentMessage.ApplicationProperties.Should().HaveCount(3);
        sentMessage.ApplicationProperties["CustomProperty1"].Should().Be("Value1");
        sentMessage.ApplicationProperties["CustomProperty2"].Should().Be(42);
        sentMessage.ApplicationProperties["CustomProperty3"].Should().Be(true);
    }

    [Fact]
    public async Task SendQueueMessage_WhenSenderThrowsException_ShouldReturnFailure()
    {
        var message = new TestQueueMessage
        {
            QueueName = "error-queue",
            TestData = "This will fail"
        };

        MockServiceBusTestHelper.SetupSendFailure("error-queue");

        var result = await MessagingProvider.SendQueueMessage(message);

        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().NotBeNull();
        result.Failure!.Title.Should().Be("Error");
        result.Failure.Description.Should().Be("There was an error when processing the request");
    }

    [Fact]
    public async Task SendMultipleMessages_ShouldCaptureAllMessages()
    {
        var queueMessage1 = new TestQueueMessage { QueueName = "queue1", TestData = "Queue message 1" };
        var queueMessage2 = new TestQueueMessage { QueueName = "queue2", TestData = "Queue message 2" };
        var topicMessage = new TestTopicMessage { TopicName = "topic1", TestData = "Topic message 1" };

        await MessagingProvider.SendQueueMessage(queueMessage1);
        await MessagingProvider.SendQueueMessage(queueMessage2);
        await MessagingProvider.SendTopicMessage(topicMessage);

        MockServiceBusTestHelper.TotalSentMessageCount.Should().Be(3);
        MockServiceBusTestHelper.GetMessageCountForEntity("queue1").Should().Be(1);
        MockServiceBusTestHelper.GetMessageCountForEntity("queue2").Should().Be(1);
        MockServiceBusTestHelper.GetMessageCountForEntity("topic1").Should().Be(1);

        var allQueueMessages = MockServiceBusTestHelper.GetSentMessages<TestQueueMessage>();
        allQueueMessages.Should().HaveCount(2);

        var allTopicMessages = MockServiceBusTestHelper.GetSentMessages<TestTopicMessage>();
        allTopicMessages.Should().HaveCount(1);
    }

    [Fact]
    public async Task ClearSentMessages_ShouldRemoveAllCapturedMessages()
    {
        var message = new TestQueueMessage { QueueName = "test-queue", TestData = "Test" };

        await MessagingProvider.SendQueueMessage(message);
        MockServiceBusTestHelper.TotalSentMessageCount.Should().Be(1);

        MockServiceBusTestHelper.ClearSentMessages();

        MockServiceBusTestHelper.TotalSentMessageCount.Should().Be(0);
        MockServiceBusTestHelper.GetSentMessages().Should().BeEmpty();
    }

    [Fact]
    public async Task VerifyMessageSent_WithCorrectCount_ShouldNotThrow()
    {
        var message = new TestQueueMessage { QueueName = "verify-queue", TestData = "Verify test" };

        await MessagingProvider.SendQueueMessage(message);

        MockServiceBusTestHelper.VerifyMessageSent("verify-queue");
    }

    [Fact]
    public async Task VerifyMessageSent_WithIncorrectCount_ShouldThrow()
    {
        var message = new TestQueueMessage { QueueName = "verify-queue", TestData = "Verify test" };

        await MessagingProvider.SendQueueMessage(message);

        var action = () => MockServiceBusTestHelper.VerifyMessageSent("verify-queue", 2);
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Expected 2 messages to verify-queue, but found 1");
    }
}