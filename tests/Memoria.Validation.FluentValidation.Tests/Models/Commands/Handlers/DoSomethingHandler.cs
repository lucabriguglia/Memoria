using Memoria.Commands;
using Memoria.Results;

namespace Memoria.Validation.FluentValidation.Tests.Models.Commands.Handlers;

public class DoSomethingHandler : ICommandHandler<DoSomething>
{
    public async Task<Result> Handle(DoSomething command, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        return Result.Ok();
    }
}
