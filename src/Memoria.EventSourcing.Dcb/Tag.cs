namespace Memoria.EventSourcing.Dcb;

/// <summary>
/// A single key/value label attached to an event. Tags are the unit of cross-stream
/// correlation in Dynamic Consistency Boundary (DCB) event sourcing.
/// </summary>
public readonly record struct Tag(string Key, string Value);
