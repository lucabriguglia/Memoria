using Memoria.Commands;

namespace Memoria.Validation.FluentValidation.Tests.Models.Commands;

public record DoSomethingWithCommandResponse(string Name) : ICommand<CommandResponse>;
