using Memoria.Commands;
using Memoria.Results;

namespace Memoria.Tests.Models.Commands.Handlers;

public class SimpleCommandHandler : ICommandHandler<SimpleCommand>
{
    public Task<Result> Handle(SimpleCommand command, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Ok());
    }
}
