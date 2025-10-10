using Memoria.Commands;

namespace Memoria.Examples.EventSourcing.Cosmos.Commands;

public record PlaceOrderCommand(Guid CustomerId, Guid OrderId, decimal Amount) : ICommand;
