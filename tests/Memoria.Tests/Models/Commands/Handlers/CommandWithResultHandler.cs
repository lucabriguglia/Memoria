using Memoria.Commands;
using Memoria.Results;

namespace Memoria.Tests.Models.Commands.Handlers;

public class CommandWithResultHandler : ICommandHandler<CommandWithResult, string>
{
    public Task<Result<string>> Handle(CommandWithResult command, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<Result<string>>(command.Name);
    }
}
