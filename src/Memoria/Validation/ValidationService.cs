using Memoria.Commands;
using Memoria.Results;

namespace Memoria.Validation;

/// <summary>
/// Represents a validation service that provides methods to validate commands.
/// </summary>
public class ValidationService(IValidationProvider validationProvider) : IValidationService
{
    /// <summary>
    /// Validates a command and returns a result indicating the success or failure of the validation process.
    /// </summary>
    /// <typeparam name="TCommand">The type of the command being validated. It must implement the <see cref="ICommand"/> interface.</typeparam>
    /// <param name="command">The command to validate. Must not be null.</param>
    /// <returns>A <see cref="Task{Result}"/> representing the result of the validation. The result indicates whether the validation succeeded or failed with details.</returns>
    public async Task<Result> Validate<TCommand>(TCommand command) where TCommand : ICommand
    {
        ArgumentNullException.ThrowIfNull(command);

        var validationResponse = await validationProvider.Validate(command);

        return !validationResponse.IsValid
            ? Result.Fail(ErrorCode.BadRequest, title: "Validation Failed", description: BuildErrorMessage(validationResponse.Errors))
            : Result.Ok();
    }

    /// <summary>
    /// Validates the provided command and returns a result indicating whether the validation succeeded or failed.
    /// </summary>
    /// <typeparam name="TResponse">The type of the response expected from the command.</typeparam>
    /// <param name="command">The command to validate. Must not be null.</param>
    /// <returns>A <see cref="Task{Result{TResponse}}"/> representing the validation outcome. If the validation fails, a failure result with details is returned; otherwise, a success result.</returns>
    public async Task<Result<TResponse>> Validate<TResponse>(ICommand<TResponse> command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var validationResponse = await validationProvider.Validate(command);

        return !validationResponse.IsValid
            ? Result<TResponse>.Fail(ErrorCode.BadRequest, title: "Validation Failed", description: BuildErrorMessage(validationResponse.Errors))
            : Result<TResponse>.Ok(new Success<TResponse>());
    }

    private static string BuildErrorMessage(IEnumerable<ValidationError> validationErrors)
    {
        var errorMessages = validationErrors.Select(ve => ve.ErrorMessage).ToArray();
        return $"Validation failed with errors: {string.Join("; ", errorMessages)}";
    }
}
