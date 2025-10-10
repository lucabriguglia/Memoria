using Memoria.Commands;
using Memoria.Results;
using Memoria.Tests.Models.Notifications;

namespace Memoria.Tests.Models.Commands.Handlers;

public class DoSomethingHandler : ICommandHandler<DoSomething, CommandResponse>
{
    public async Task<Result<CommandResponse>> Handle(DoSomething command, CancellationToken cancellationToken = default)
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
