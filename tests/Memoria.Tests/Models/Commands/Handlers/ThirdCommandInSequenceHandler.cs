using Memoria.Commands;
using Memoria.Results;

namespace Memoria.Tests.Models.Commands.Handlers;

public class ThirdCommandInSequenceHandler : ICommandSequenceHandler<ThirdCommandInSequence, string>
{
    public async Task<Result<string>> Handle(ThirdCommandInSequence command, IEnumerable<Result<string>> previousResults, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        return $"Third command result; {string.Join(", ", previousResults.Select(r => r.Value))}";
    }
}
