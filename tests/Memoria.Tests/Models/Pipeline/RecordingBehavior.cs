using Memoria.Pipeline;
using Memoria.Results;

namespace Memoria.Tests.Models.Pipeline;

public class RecordingBehavior<TRequest, TResponse>(string name, List<string> calls)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<Result<TResponse>> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        calls.Add($"{name}:enter");
        var result = await next();
        calls.Add($"{name}:exit");
        return result;
    }
}
