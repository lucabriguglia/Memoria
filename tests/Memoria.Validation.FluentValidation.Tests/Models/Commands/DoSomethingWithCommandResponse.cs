using Memoria.Commands;

namespace OpenCqrs.Validation.FluentValidation.Tests.Models.Commands;

public record DoSomethingWithCommandResponse(string Name) : ICommand<CommandResponse>;
