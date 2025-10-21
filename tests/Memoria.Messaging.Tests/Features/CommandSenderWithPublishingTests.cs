using FluentAssertions;
using Memoria.Messaging.Tests.Models.Commands;
using Memoria.Messaging.Tests.Models.Messages;
using Xunit;

namespace Memoria.Messaging.Tests.Features;

public abstract class CommandSenderWithPublishingTests(IMessagingProviderFactory messagingProviderFactory)
    : TestBase(messagingProviderFactory)
{
    [Fact]
    public async Task SendAndPublish_ShouldPublishServiceBusMessages()
    {
        var result = await Dispatcher.SendAndPublish(new DoSomething(Id: Guid.NewGuid(), Name: "Test message data"));

        result.Should().NotBeNull();
        result.CommandResult.IsSuccess.Should().BeTrue();

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
}