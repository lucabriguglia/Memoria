using Memoria.Commands;

namespace OpenCqrs.Messaging.ServiceBus.Tests.Models.Commands;

public record DoSomething(Guid Id, string Name) : ICommand<CommandResponse>;
