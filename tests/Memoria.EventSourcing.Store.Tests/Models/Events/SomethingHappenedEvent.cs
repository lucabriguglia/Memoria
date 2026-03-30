using Memoria.EventSourcing.Domain;

namespace Memoria.EventSourcing.Store.Tests.Models.Events;

[EventType("SomethingHappened")]
public record SomethingHappenedEvent(string Something) : IEvent;

[EventType("SomethingHappened", version: 2)]
public record SomethingHappenedEvent2(string Something2) : IEvent;
