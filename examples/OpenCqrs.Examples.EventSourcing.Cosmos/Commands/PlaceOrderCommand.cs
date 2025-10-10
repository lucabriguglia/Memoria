using Memoria.Commands;

namespace OpenCqrs.Examples.EventSourcing.Cosmos.Commands;

public record PlaceOrderCommand(Guid CustomerId, Guid OrderId, decimal Amount) : ICommand;
