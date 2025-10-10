using Memoria.Queries;
using Memoria.Results;

namespace Memoria.Examples.Caching.Memory.Queries.Handlers;

public class GetSomethingQueryHandler : IQueryHandler<GetSomethingQuery, string>
{
    public async Task<Result<string>> Handle(GetSomethingQuery query, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return "Hello from GetSomethingQueryHandler";
    }
}
