using FluentAssertions;
using FluentAssertions.Execution;
using Memoria.Validation.FluentValidation.Tests.Models.Commands;
using Xunit;

namespace Memoria.Validation.FluentValidation.Tests.Features;

public class FluentValidationTests : TestBase
{
    [Fact]
    public async Task Send_Should_Validate_The_Command_And_Return_Failure_If_Command_Is_Not_Valid()
    {
        var result = await Dispatcher.Send(command: new DoSomething(Name: string.Empty), validateCommand: true);

        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeFalse();
            result.Failure.Should().NotBeNull();
            result.Failure.Title.Should().Be("Validation Failed");
            result.Failure.Description.Should().Be("Validation failed with errors: Name is required.");
        }
    }

    [Fact]
    public async Task Send_Should_Validate_The_Command_And_Return_Success_If_Command_Is_Valid()
    {
        var result = await Dispatcher.Send(command: new DoSomething(Name: "Test Name"), validateCommand: true);

        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue();
            result.Failure.Should().BeNull();
        }
    }

    [Fact]
    public async Task SendWithResponse_Should_Validate_The_Command_And_Return_Failure_If_Command_Is_Not_Valid()
    {
        var result = await Dispatcher.Send(command: new DoSomethingWithResponse(Name: string.Empty), validateCommand: true);

        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeFalse();
            result.Failure.Should().NotBeNull();
            result.Failure.Title.Should().Be("Validation Failed");
            result.Failure.Description.Should().Be("Validation failed with errors: Name is required.");
        }
    }

    [Fact]
    public async Task SendWithResponse_Should_Validate_The_Command_And_Return_Success_If_Command_Is_Valid()
    {
        var result = await Dispatcher.Send(command: new DoSomethingWithResponse(Name: "Test Name"), validateCommand: true);

        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue();
            result.Failure.Should().BeNull();
        }
    }

    [Fact]
    public async Task SendAndPublish_Should_Validate_The_Command_And_Return_Failure_If_Command_Is_Not_Valid()
    {
        var result = await Dispatcher.SendAndPublish(command: new DoSomethingWithCommandResponse(Name: string.Empty), validateCommand: true);

        using (new AssertionScope())
        {
            result.CommandResult.IsSuccess.Should().BeFalse();
            result.CommandResult.Failure.Should().NotBeNull();
            result.CommandResult.Failure.Title.Should().Be("Validation Failed");
            result.CommandResult.Failure.Description.Should().Be("Validation failed with errors: Name is required.");
        }
    }

    [Fact]
    public async Task SendAndPublish_Should_Validate_The_Command_And_Return_Success_If_Command_Is_Valid()
    {
        var result = await Dispatcher.SendAndPublish(command: new DoSomethingWithCommandResponse(Name: "Test Name"), validateCommand: true);

        using (new AssertionScope())
        {
            result.CommandResult.IsSuccess.Should().BeTrue();
            result.CommandResult.Failure.Should().BeNull();
        }
    }

    [Fact]
    public async Task Send_Command_Sequence_Should_Validate_The_Commands_And_Return_Success_If_Commands_Are_Valid()
    {
        var commandSequence = new TestCommandSequence();
        commandSequence.AddCommand(new FirstCommandInSequence(Name: "Test Name"));
        commandSequence.AddCommand(new SecondCommandInSequence(Name: "Test Name"));
        var sendResult = await Dispatcher.Send(commandSequence, validateCommands: true);
        var results = sendResult.ToList();

        using (new AssertionScope())
        {
            results.Count.Should().Be(2);
            results[0].IsSuccess.Should().BeTrue();
            results[0].Failure.Should().BeNull();
            results[1].IsSuccess.Should().BeTrue();
            results[1].Failure.Should().BeNull();
        }
    }

    [Fact]
    public async Task Send_Command_Sequence_Should_Validate_The_Commands_And_Return_Failure_If_Commands_Are_Not_Valid()
    {
        var commandSequence = new TestCommandSequence();
        commandSequence.AddCommand(new FirstCommandInSequence(Name: string.Empty));
        commandSequence.AddCommand(new SecondCommandInSequence(Name: "Test Name"));
        var sendResult = await Dispatcher.Send(commandSequence, validateCommands: true);
        var results = sendResult.ToList();

        using (new AssertionScope())
        {
            results.Count.Should().Be(2);
            results[0].IsSuccess.Should().BeFalse();
            results[0].Failure.Should().NotBeNull();
            results[1].IsSuccess.Should().BeTrue();
            results[1].Failure.Should().BeNull();
        }
    }

    [Fact]
    public async Task Send_Command_Sequence_Should_Validate_The_Commands_And_Stop_Processing_On_First_Failure()
    {
        var commandSequence = new TestCommandSequence();
        commandSequence.AddCommand(new FirstCommandInSequence(Name: string.Empty));
        commandSequence.AddCommand(new SecondCommandInSequence(Name: "Test Name"));
        var sendResult = await Dispatcher.Send(commandSequence, validateCommands: true, stopProcessingOnFirstFailure: true);
        var results = sendResult.ToList();

        using (new AssertionScope())
        {
            results.Count.Should().Be(1);
            results[0].IsSuccess.Should().BeFalse();
            results[0].Failure.Should().NotBeNull();
        }
    }
}
