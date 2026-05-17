using Memoria.EventSourcing.Domain;

namespace Memoria.EventSourcing.Dcb;

/// <summary>
/// An event as it sits in the log: identity, total-order position, payload, and tags.
/// </summary>
public sealed record StoredEvent(
    string EventId,
    long GlobalPosition,
    IEvent Payload,
    TagSet Tags,
    DateTimeOffset RecordedAt);
