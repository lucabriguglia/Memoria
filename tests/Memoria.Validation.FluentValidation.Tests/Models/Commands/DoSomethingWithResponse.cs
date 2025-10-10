using Memoria.Commands;

namespace Memoria.Validation.FluentValidation.Tests.Models.Commands;

public record DoSomethingWithResponse(string Name) : ICommand<string>;
