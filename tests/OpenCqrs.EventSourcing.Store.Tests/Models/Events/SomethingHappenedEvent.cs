using OpenCqrs.EventSourcing.Domain;

namespace OpenCqrs.EventSourcing.Store.Tests.Models.Events;

[EventType("SomethingHappened")]
public record SomethingHappenedEvent(string Something) : IEvent;
