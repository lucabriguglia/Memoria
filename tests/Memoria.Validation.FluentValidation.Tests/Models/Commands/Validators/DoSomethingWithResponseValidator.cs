using FluentValidation;

namespace OpenCqrs.Validation.FluentValidation.Tests.Models.Commands.Validators;

public class DoSomethingWithResponseValidator : AbstractValidator<DoSomethingWithResponse>
{
    public DoSomethingWithResponseValidator()
    {
        RuleFor(c => c.Name).NotEmpty().WithMessage("Name is required.");
    }
}
