using Memoria.Commands;

namespace Memoria.Examples.Messaging.ServiceBus.Commands;

public record PlaceOrderCommand(Guid CustomerId, Guid OrderId, decimal Amount) : ICommand<CommandResponse>;
