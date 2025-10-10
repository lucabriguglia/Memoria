using FluentValidation;

namespace OpenCqrs.Validation.FluentValidation.Tests.Models.Commands.Validators;

public class SecondCommandInSequenceValidator : AbstractValidator<SecondCommandInSequence>
{
    public SecondCommandInSequenceValidator()
    {
        RuleFor(c => c.Name).NotEmpty().WithMessage("Name is required.");
    }
}
