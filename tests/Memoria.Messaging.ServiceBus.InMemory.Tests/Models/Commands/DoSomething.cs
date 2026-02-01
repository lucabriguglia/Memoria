using Memoria.Commands;

namespace Memoria.Messaging.ServiceBus.InMemory.Tests.Models.Commands;

public record DoSomething(Guid Id, string Name) : ICommand<CommandResponse>;
