using System.Collections.ObjectModel;

namespace Memoria.Commands;

/// <summary>
/// Abstract base class for command sequences that execute multiple commands and return a response.
/// </summary>
/// <typeparam name="TResponse">The type of response returned by the command sequence.</typeparam>
public abstract class CommandSequence<TResponse> : ICommandSequence<TResponse>
{
    private readonly List<ICommand<TResponse>> _commands = [];

    /// <summary>
    /// Gets the read-only collection of commands in the sequence.
    /// </summary>
    public ReadOnlyCollection<ICommand<TResponse>> Commands => _commands.AsReadOnly();

    /// <summary>
    /// Adds the command to the sequence collection.
    /// </summary>
    /// <param name="command">The command.</param>
    public void AddCommand(ICommand<TResponse> command)
    {
        _commands.Add(command);
    }
}
