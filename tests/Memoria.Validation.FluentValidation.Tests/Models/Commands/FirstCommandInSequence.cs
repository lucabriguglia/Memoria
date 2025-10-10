using Memoria.Commands;

namespace OpenCqrs.Validation.FluentValidation.Tests.Models.Commands;

public record FirstCommandInSequence(string Name) : ICommand<string>;
