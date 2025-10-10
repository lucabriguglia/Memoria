using Memoria.Commands;
using Memoria.Results;

namespace Memoria.Validation;

/// <summary>
/// Provides methods for validating commands.
/// </summary>
public interface IValidationService
{
    /// <summary>
    /// Validates the given command and returns the result of the validation.
    /// </summary>
    /// <typeparam name="TCommand">The type of the command that implements ICommand.</typeparam>
    /// <param name="command">The command to be validated.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a validation result which can be either success or failure.</returns>
    Task<Result> Validate<TCommand>(TCommand command) where TCommand : ICommand;

    /// <summary>
    /// Validates the given command and returns the result of the validation.
    /// </summary>
    /// <typeparam name="TResponse">The type of the response returned by the command.</typeparam>
    /// <param name="command">The command to be validated.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a validation result which can be either success or failure.</returns>
    Task<Result<TResponse>> Validate<TResponse>(ICommand<TResponse> command);
}
