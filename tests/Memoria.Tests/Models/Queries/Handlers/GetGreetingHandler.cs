using Memoria.Queries;
using Memoria.Results;

namespace Memoria.Tests.Models.Queries.Handlers;

public class GetGreetingHandler : IQueryHandler<GetGreeting, string>
{
    public Task<Result<string>> Handle(GetGreeting query, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<Result<string>>(new Success<string>($"Hello, {query.Name}"));
    }
}
