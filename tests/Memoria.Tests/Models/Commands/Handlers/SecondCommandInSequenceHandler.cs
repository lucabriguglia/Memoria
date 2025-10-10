using Memoria.Commands;
using Memoria.Results;

namespace Memoria.Tests.Models.Commands.Handlers;

public class SecondCommandInSequenceHandler : ICommandSequenceHandler<SecondCommandInSequence, string>
{
    public async Task<Result<string>> Handle(SecondCommandInSequence command, IEnumerable<Result<string>> previousResults, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        return new Failure();
    }
}
