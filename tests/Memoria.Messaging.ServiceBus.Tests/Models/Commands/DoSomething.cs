using Memoria.Commands;

namespace Memoria.Messaging.ServiceBus.Tests.Models.Commands;

public record DoSomething(Guid Id, string Name) : ICommand<CommandResponse>;
