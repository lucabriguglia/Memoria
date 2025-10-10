using FluentValidation;
using FluentValidation.Results;
using Memoria.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace OpenCqrs.Validation.FluentValidation;

internal class CommandValidatorWrapper<TCommand, TResponse> : CommandValidatorWrapperBase<TResponse> where TCommand : ICommand<TResponse>
{
    public override async Task<ValidationResult> Validate(ICommand<TResponse> command, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var validator = GetValidator<IValidator<TCommand>>(serviceProvider);
        if (validator == null)
        {
            throw new InvalidOperationException("Command validator not found.");
        }

        return await validator.ValidateAsync((TCommand)command, cancellationToken);
    }
}

internal abstract class CommandValidatorWrapperBase<TResult>
{
    protected static TValidator? GetValidator<TValidator>(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetService<TValidator>();
    }

    public abstract Task<ValidationResult> Validate(ICommand<TResult> command, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}
