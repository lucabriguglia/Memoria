using FluentAssertions;
using FluentAssertions.Execution;
using Memoria.Tests.Models.Commands;
using Xunit;

namespace Memoria.Tests.Features;

public class CommandSequenceTests : TestBase
{
    [Fact]
    public async Task Send_Command_Sequence_Processes_All_Commands()
    {
        var sendResult = await Dispatcher.Send(new TestCommandSequence());
        var results = sendResult.ToList();

        using (new AssertionScope())
        {
            results.Count.Should().Be(3);

            results[0].IsSuccess.Should().BeTrue();
            results[0].Value.Should().Be("First command result");

            results[1].IsSuccess.Should().BeFalse();
            results[1].Value.Should().BeNull();

            results[2].IsSuccess.Should().BeTrue();
            results[2].Value.Should().Be("Third command result; First command result, ");
        }
    }

    [Fact]
    public async Task Send_Command_Sequence_Stops_Processing_On_First_Failure()
    {
        var sendResult = await Dispatcher.Send(new TestCommandSequence(), stopProcessingOnFirstFailure: true);
        var results = sendResult.ToList();

        using (new AssertionScope())
        {
            results.Count.Should().Be(2);

            results[0].IsSuccess.Should().BeTrue();
            results[0].Value.Should().Be("First command result");

            results[1].IsSuccess.Should().BeFalse();
            results[1].Value.Should().BeNull();
        }
    }
}
