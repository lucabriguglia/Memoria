using Memoria.EventSourcing.Domain;

namespace OpenCqrs.Examples.EventSourcing.Cosmos.events;

[EventType("OrderPlaced")]
public record OrderPlacedEvent(Guid OrderId, decimal Amount) : IEvent;
