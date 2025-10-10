using Memoria.Commands;

namespace Memoria.Tests.Models.Commands;

public record DoSomething(string Name) : ICommand<CommandResponse>;
