using Memoria.EventSourcing.Domain;

namespace Memoria.EventSourcing.Store.Tests.Models.Events;

[EventType("SomethingHappened")]
public record SomethingHappenedEvent(string Something) : IEvent;
