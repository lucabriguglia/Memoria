using Memoria.Commands;

namespace Memoria.Validation;

/// <summary>
/// Provides validation functionality for commands.
/// </summary>
public interface IValidationProvider
{
    /// <summary>
    /// Executes validation for the provided command.
    /// </summary>
    /// <typeparam name="TCommand">The type of the command to validate.</typeparam>
    /// <param name="command">The command that requires validation.</param>
    /// <param name="cancellationToken">Optional. A token that can be used to signal cancellation of the operation.</param>
    /// <returns>A <see cref="ValidationResponse"/> indicating whether the validation succeeded or failed, including any validation errors.</returns>
    Task<ValidationResponse> Validate<TCommand>(TCommand command, CancellationToken cancellationToken = default) where TCommand : ICommand;

    /// <summary>
    /// Executes validation for the provided command.
    /// </summary>
    /// <typeparam name="TResponse">The type of the command to validate.</typeparam>
    /// <param name="command">The command that requires validation.</param>
    /// <param name="cancellationToken">Optional. A token that can be used to signal cancellation of the operation.</param>
    /// <returns>A <see cref="ValidationResponse"/> indicating whether the validation was successful, along with any validation errors.</returns>
    Task<ValidationResponse> Validate<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default);
}
