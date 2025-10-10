using Memoria.EventSourcing.Domain;

namespace Memoria.Examples.EventSourcing.Cosmos.DomainEvents;

[EventType("OrderPlaced")]
public record OrderPlacedEvent(Guid OrderId, decimal Amount) : IEvent;
