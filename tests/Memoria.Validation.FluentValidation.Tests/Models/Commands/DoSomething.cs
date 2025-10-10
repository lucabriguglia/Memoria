using Memoria.Commands;

namespace Memoria.Validation.FluentValidation.Tests.Models.Commands;

public record DoSomething(string Name) : ICommand;
