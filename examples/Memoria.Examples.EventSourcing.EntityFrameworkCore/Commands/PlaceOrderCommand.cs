using Memoria.Commands;

namespace OpenCqrs.Examples.EventSourcing.EntityFrameworkCore.Commands;

public record PlaceOrderCommand(Guid CustomerId, Guid OrderId, decimal Amount) : ICommand;
