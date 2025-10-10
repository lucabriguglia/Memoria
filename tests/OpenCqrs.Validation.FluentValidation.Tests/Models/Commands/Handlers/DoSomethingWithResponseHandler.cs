using Memoria.Commands;
using Memoria.Results;

namespace OpenCqrs.Validation.FluentValidation.Tests.Models.Commands.Handlers;

public class DoSomethingWithResponseHandler : ICommandHandler<DoSomethingWithResponse, string>
{
    public async Task<Result<string>> Handle(DoSomethingWithResponse command, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        return "Hello";
    }
}
