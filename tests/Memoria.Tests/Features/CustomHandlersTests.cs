using FluentAssertions;
using FluentAssertions.Execution;
using Memoria.Tests.Models.Commands;
using Memoria.Tests.Models.Commands.Handlers;
using Xunit;

namespace Memoria.Tests.Features;

public class CustomHandlersTests : TestBase
{
    [Fact]
    public async Task Send_WithCustomHandler_ShouldWorkAsExpected()
    {
        var command = new SimpleCommand("TestName");
        var result = await Dispatcher.Send(command, () => new CustomService().Simple(command));

        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue();
        }
    }

    [Fact]
    public async Task SendWithResult_WithCustomHandler_ShouldWorkAsExpected()
    {
        var command = new CommandWithResult("TestName");
        var result = await Dispatcher.Send(command, () => new CustomService().WithResult(command));

        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be("Hello TestName");
        }
    }

    [Fact]
    public async Task SendAndPublish_WithCustomHandler_ShouldWorkAsExpected()
    {
        var command = new DoSomething("TestName");
        var result = await Dispatcher.SendAndPublish(command, () => new CustomService().DoSomething(command));

        using (new AssertionScope())
        {
            result.CommandResult.Value.Should().NotBeNull();
            result.CommandResult.Value.Notifications.Should().NotBeNull();
            result.CommandResult.Value.Notifications.Count().Should().Be(1);

            result.NotificationResults.Should().NotBeNull();
            result.NotificationResults.Count().Should().Be(2);
            result.NotificationResults.All(nr => nr.IsSuccess).Should().BeTrue();
        }
    }
}
