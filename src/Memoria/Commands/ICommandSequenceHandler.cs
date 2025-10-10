using Memoria.Results;

namespace Memoria.Commands;

/// <summary>
/// Represents a handler capable of processing commands in a sequence.
/// </summary>
/// <typeparam name="TCommand">The type of command being handled. This must implement <see cref="ICommand{TResponse}" />.</typeparam>
/// <typeparam name="TResponse">The type of response returned upon handling the command.</typeparam>
public interface ICommandSequenceHandler<in TCommand, TResponse> where TCommand : ICommand<TResponse>
{
    /// <summary>
    /// Handles the execution of a command within a command sequence. Processes the provided command and produces a result
    /// based on the given input and the results of preceding commands in the sequence.
    /// </summary>
    /// <param name="command">The command to be handled within the sequence.</param>
    /// <param name="previousResults">The results of commands that have been executed previously in the sequence.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the outcome of the command execution.</returns>
    Task<Result<TResponse>> Handle(TCommand command, IEnumerable<Result<TResponse>> previousResults, CancellationToken cancellationToken = default);
}
