using Memoria.Commands;

namespace Memoria.Validation.FluentValidation.Tests.Models.Commands;

public record FirstCommandInSequence(string Name) : ICommand<string>;
