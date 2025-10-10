using FluentValidation;

namespace OpenCqrs.Validation.FluentValidation.Tests.Models.Commands.Validators;

public class DoSomethingValidator : AbstractValidator<DoSomething>
{
    public DoSomethingValidator()
    {
        RuleFor(c => c.Name).NotEmpty().WithMessage("Name is required.");
    }
}
