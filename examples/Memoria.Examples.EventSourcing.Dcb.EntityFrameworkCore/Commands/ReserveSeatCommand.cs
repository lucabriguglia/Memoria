using Memoria.Commands;

namespace Memoria.Examples.EventSourcing.Dcb.EntityFrameworkCore.Commands;

public sealed record ReserveSeatCommand(string ShowId, string Seat, Guid CustomerId) : ICommand;
