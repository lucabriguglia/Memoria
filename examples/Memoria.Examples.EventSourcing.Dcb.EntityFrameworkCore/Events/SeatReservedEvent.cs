using Memoria.EventSourcing.Domain;

namespace Memoria.Examples.EventSourcing.Dcb.EntityFrameworkCore.Events;

[EventType("SeatReserved")]
public sealed record SeatReservedEvent(string ShowId, string Seat, Guid CustomerId) : IEvent;
