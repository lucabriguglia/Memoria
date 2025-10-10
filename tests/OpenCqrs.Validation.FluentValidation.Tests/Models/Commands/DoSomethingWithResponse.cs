using Memoria.Commands;

namespace OpenCqrs.Validation.FluentValidation.Tests.Models.Commands;

public record DoSomethingWithResponse(string Name) : ICommand<string>;
