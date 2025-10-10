using Memoria.Commands;
using Memoria.Results;

namespace Memoria.Validation.FluentValidation.Tests.Models.Commands.Handlers;

public class DoSomethingWithCommandResponseHandler : ICommandHandler<DoSomethingWithCommandResponse, CommandResponse>
{
    public async Task<Result<CommandResponse>> Handle(DoSomethingWithCommandResponse command, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        return new CommandResponse();
    }
}
