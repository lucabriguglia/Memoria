using Memoria.EventSourcing.Domain;

namespace OpenCqrs.Examples.EventSourcing.Cosmos.DomainEvents;

[EventType("OrderPlaced")]
public record OrderPlacedEvent(Guid OrderId, decimal Amount) : IEvent;
