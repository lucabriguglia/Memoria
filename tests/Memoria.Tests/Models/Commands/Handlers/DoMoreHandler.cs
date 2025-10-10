using Memoria.Commands;
using Memoria.Results;
using OpenCqrs.Tests.Models.Notifications;

namespace OpenCqrs.Tests.Models.Commands.Handlers;

public class DoMoreHandler : ICommandHandler<DoMore, CommandResponse>
{
    public async Task<Result<CommandResponse>> Handle(DoMore command, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        var somethingHappened = new SomethingHappened(command.Name);
        var somethingElseHappened = new SomethingElseHappened(command.Name);

        var response = new CommandResponse(
            [somethingHappened, somethingElseHappened],
            new { Message = $"Successfully processed command for: {command.Name}" }
        );

        return response;
    }
}
