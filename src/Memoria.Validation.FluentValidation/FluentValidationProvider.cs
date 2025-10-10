using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using FluentValidation;
using FluentValidation.Results;
using Memoria.Commands;
using Memoria.Validation;

namespace OpenCqrs.Validation.FluentValidation;

/// <summary>
/// Provides validation functionality using FluentValidation.
/// </summary>
/// <remarks>
/// This class implements the <see cref="IValidationProvider"/> interface and is responsible for validating command objects.
/// It performs validation asynchronously and uses registered FluentValidation validators.
/// </remarks>
public class FluentValidationProvider(IServiceProvider serviceProvider) : IValidationProvider
{
    private static readonly ConcurrentDictionary<Type, object?> CommandValidatorWrappers = new();

    /// <summary>
    /// Validates the specified command using FluentValidation.
    /// </summary>
    /// <typeparam name="TCommand">The type of the command to validate. Must implement the <see cref="ICommand"/> interface.</typeparam>
    /// <param name="command">The command object to be validated.</param>
    /// <param name="cancellationToken">A token to observe cancellation requests.</param>
    /// <returns>A <see cref="ValidationResponse"/> representing the result of the validation process.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="command"/> is null.</exception>
    /// <exception cref="Exception">Thrown when no validator is found for the provided command type.</exception>
    public async Task<ValidationResponse> Validate<TCommand>(TCommand command, CancellationToken cancellationToken = default) where TCommand : ICommand
    {
        ArgumentNullException.ThrowIfNull(command);

        var validator = serviceProvider.GetService<IValidator<TCommand>>();
        if (validator is null)
        {
            throw new InvalidOperationException($"Validator for {typeof(TCommand).Name} not found.");
        }

        var validationResult = await validator.ValidateAsync(command, cancellationToken);

        return BuildValidationResponse(validationResult);
    }

    /// <summary>
    /// Validates the specified command using FluentValidation.
    /// </summary>
    /// <typeparam name="TResponse">The type of the response the command produces.</typeparam>
    /// <param name="command">The command to be validated. Must implement the <see cref="ICommand{TResponse}"/> interface.</param>
    /// <param name="cancellationToken">A token to observe cancellation requests.</param>
    /// <returns>A <see cref="ValidationResponse"/> representing the result of the validation process.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="command"/> is null.</exception>
    /// <exception cref="Exception">Thrown when no validator is found for the specified command type.</exception>
    public async Task<ValidationResponse> Validate<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var commandType = command.GetType();

        var validator = (CommandValidatorWrapperBase<TResponse>)CommandValidatorWrappers.GetOrAdd(commandType, _ =>
            Activator.CreateInstance(typeof(CommandValidatorWrapper<,>).MakeGenericType(commandType, typeof(TResponse))))!;

        if (validator is null)
        {
            throw new InvalidOperationException($"Validator for {typeof(ICommand<TResponse>).Name} not found.");
        }

        var validationResult = await validator.Validate(command, serviceProvider, cancellationToken);

        return BuildValidationResponse(validationResult);
    }

    private static ValidationResponse BuildValidationResponse(ValidationResult validationResult)
    {
        return new ValidationResponse
        {
            Errors = validationResult.Errors.Select(failure => new ValidationError
            {
                PropertyName = failure.PropertyName,
                ErrorMessage = failure.ErrorMessage
            }).ToList()
        };
    }
}
