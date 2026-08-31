using Memoria.EventSourcing.Domain;

namespace Memoria.EventSourcing.Dcb.Tests.Models.Events;

[EventType("SeatReserved")]
public record SeatReservedEvent(string SeatId, string StudentId) : IEvent;

[EventType("SeatReleased")]
public record SeatReleasedEvent(string SeatId) : IEvent;

[EventType("Unrelated")]
public record UnrelatedEvent(string Noise) : IEvent;
