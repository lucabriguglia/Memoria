using Memoria.Commands;

namespace OpenCqrs.Examples.Messaging.ServiceBus.Commands;

public record PlaceOrderCommand(Guid CustomerId, Guid OrderId, decimal Amount) : ICommand<CommandResponse>;
