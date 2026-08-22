using Memoria.EventSourcing.Domain;

namespace Memoria.EventSourcing.Tests.Models.Events;

[EventType("ItemCreated")]
public record ItemCreatedEvent(string Id, string Name) : IEvent;
