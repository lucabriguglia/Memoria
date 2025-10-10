using Memoria.Commands;

namespace OpenCqrs.Validation.FluentValidation.Tests.Models.Commands;

public record DoSomething(string Name) : ICommand;
