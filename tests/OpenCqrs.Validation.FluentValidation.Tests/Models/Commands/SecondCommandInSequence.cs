using Memoria.Commands;

namespace OpenCqrs.Validation.FluentValidation.Tests.Models.Commands;

public record SecondCommandInSequence(string Name) : ICommand<string>;
