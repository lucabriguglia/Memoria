using Memoria.Commands;

namespace OpenCqrs.Messaging.RabbitMq.Tests.Models.Commands;

public record DoSomething(Guid Id, string Name) : ICommand<CommandResponse>;
