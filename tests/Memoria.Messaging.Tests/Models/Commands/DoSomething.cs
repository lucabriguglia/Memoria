using Memoria.Commands;

namespace Memoria.Messaging.Tests.Models.Commands;

public record DoSomething(Guid Id, string Name) : ICommand<CommandResponse>;