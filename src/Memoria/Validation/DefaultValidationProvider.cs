using OpenCqrs.Commands;

namespace OpenCqrs.Validation;

public class DefaultValidationProvider : IValidationProvider
{
    private static string NotImplementedMessage => "No validation provider has been configured. Please configure a validation provider such as FluentValidation.";

    public Task<ValidationResponse> Validate<TCommand>(TCommand command, CancellationToken cancellationToken = default) where TCommand : ICommand
    {
        throw new NotImplementedException(NotImplementedMessage);
    }

    public Task<ValidationResponse> Validate<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException(NotImplementedMessage);
    }
}
