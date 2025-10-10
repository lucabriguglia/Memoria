using Memoria.EventSourcing.Domain;

namespace OpenCqrs.Examples.EventSourcing.EntityFrameworkCore.DomainEvents;

[EventType("OrderPlaced")]
public record OrderPlacedEvent(Guid OrderId, decimal Amount) : IEvent;
