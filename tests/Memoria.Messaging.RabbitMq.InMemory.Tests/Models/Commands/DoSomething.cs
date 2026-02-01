using Memoria.Commands;

namespace Memoria.Messaging.RabbitMq.InMemory.Tests.Models.Commands;

public record DoSomething(Guid Id, string Name) : ICommand<CommandResponse>;
