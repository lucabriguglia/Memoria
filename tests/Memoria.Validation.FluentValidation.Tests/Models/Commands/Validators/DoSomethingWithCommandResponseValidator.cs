using FluentValidation;

namespace Memoria.Validation.FluentValidation.Tests.Models.Commands.Validators;

public class DoSomethingWithCommandResponseValidator : AbstractValidator<DoSomethingWithCommandResponse>
{
    public DoSomethingWithCommandResponseValidator()
    {
        RuleFor(c => c.Name).NotEmpty().WithMessage("Name is required.");
    }
}
