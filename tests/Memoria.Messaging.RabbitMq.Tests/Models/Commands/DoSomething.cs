using Memoria.Commands;

namespace Memoria.Messaging.RabbitMq.Tests.Models.Commands;

public record DoSomething(Guid Id, string Name) : ICommand<CommandResponse>;
