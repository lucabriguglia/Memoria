using Memoria.EventSourcing.Domain;

namespace Memoria.EventSourcing.Tests.Models.Events;

[EventType("ItemRenamed")]
public record ItemRenamedEvent(string Id, string Name) : IEvent;
