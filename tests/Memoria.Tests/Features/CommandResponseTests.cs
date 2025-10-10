using FluentAssertions;
using FluentAssertions.Execution;
using Memoria.Tests.Models.Commands;
using Xunit;

namespace Memoria.Tests.Features;

public class SendAndPublishTests : TestBase
{
    [Fact]
    public async Task SendAndPublish_Should_Call_NotificationHandlers_For_CommandResponse_With_SingleNotification()
    {
        var result = await Dispatcher.SendAndPublish(new DoSomething("TestName"));

        using (new AssertionScope())
        {
            result.CommandResult.Should().NotBeNull();
            result.CommandResult.Value.Should().NotBeNull();
            result.CommandResult.Value.Notifications.Should().NotBeNull();
            result.CommandResult.Value.Notifications.Count().Should().Be(1);

            result.NotificationResults.Should().NotBeNull();
            result.NotificationResults.Count().Should().Be(2);
            result.NotificationResults.All(r => r.IsSuccess).Should().BeTrue();
        }
    }

    [Fact]
    public async Task SendAndPublish_Should_Call_NotificationHandlers_For_CommandResponse_With_MultipleNotifications_And_MultipleResults()
    {
        var result = await Dispatcher.SendAndPublish(new DoMore("TestName"));

        using (new AssertionScope())
        {
            result.CommandResult.Should().NotBeNull();
            result.CommandResult.Value.Should().NotBeNull();
            result.CommandResult.Value.Notifications.Should().NotBeNull();
            result.CommandResult.Value.Notifications.Count().Should().Be(2);

            result.NotificationResults.Should().NotBeNull();
            result.NotificationResults.Count().Should().Be(3);
            result.NotificationResults.Count(r => r.IsSuccess).Should().Be(2);
            result.NotificationResults.Count(r => r.IsNotSuccess).Should().Be(1);
        }
    }
}
