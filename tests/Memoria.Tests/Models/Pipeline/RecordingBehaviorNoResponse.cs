using Memoria.Commands;
using Memoria.Pipeline;
using Memoria.Results;

namespace Memoria.Tests.Models.Pipeline;

public class RecordingBehavior<TRequest>(string name, List<string> calls)
    : IPipelineBehavior<TRequest>
    where TRequest : ICommand
{
    public async Task<Result> Handle(
        TRequest request,
        RequestHandlerDelegate next,
        CancellationToken cancellationToken)
    {
        calls.Add($"{name}:enter");
        var result = await next();
        calls.Add($"{name}:exit");
        return result;
    }
}
