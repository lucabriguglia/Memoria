using Memoria.Commands;

namespace Memoria.Validation.FluentValidation.Tests.Models.Commands;

public record SecondCommandInSequence(string Name) : ICommand<string>;
