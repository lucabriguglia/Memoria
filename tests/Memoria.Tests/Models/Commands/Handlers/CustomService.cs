using Memoria.Commands;
using Memoria.Results;
using OpenCqrs.Tests.Models.Notifications;

namespace OpenCqrs.Tests.Models.Commands.Handlers;

public class CustomService
{
    public async Task<Result> Simple(SimpleCommand command)
    {
        await Task.CompletedTask;

        return Result.Ok();
    }

    public async Task<Result<string>> WithResult(CommandWithResult command)
    {
        await Task.CompletedTask;

        return $"Hello {command.Name}";
    }

    public async Task<Result<CommandResponse>> DoSomething(DoSomething command)
    {
        await Task.CompletedTask;

        var notification = new SomethingHappened(command.Name);

        var response = new CommandResponse(
            notification,
            new { Message = $"Successfully processed command for: {command.Name}" }
        );

        return response;
    }
}
