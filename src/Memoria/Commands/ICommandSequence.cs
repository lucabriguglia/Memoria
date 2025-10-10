using System.Collections.ObjectModel;

namespace Memoria.Commands;

/// <summary>
/// Represents a sequence of commands that can be executed in a predefined order.
/// </summary>
/// <typeparam name="TResponse">The type of the response produced by executing the commands.</typeparam>
public interface ICommandSequence<TResponse>
{
    /// <summary>
    /// Gets the collection of commands that form the sequence for execution.
    /// </summary>
    ReadOnlyCollection<ICommand<TResponse>> Commands { get; }
}
