using Memoria.Commands;

namespace OpenCqrs.Tests.Models.Commands;

public record DoSomething(string Name) : ICommand<CommandResponse>;
