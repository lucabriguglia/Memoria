using Memoria.Commands;

namespace Memoria.Examples.EventSourcing.EntityFrameworkCore.Commands;

public record PlaceOrderCommand(Guid CustomerId, Guid OrderId, decimal Amount) : ICommand;
