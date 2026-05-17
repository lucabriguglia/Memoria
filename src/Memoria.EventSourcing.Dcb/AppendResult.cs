namespace Memoria.EventSourcing.Dcb;

/// <summary>
/// What an <see cref="IDcbStore"/> returns from a successful append.
/// </summary>
public sealed record AppendResult(IReadOnlyList<StoredEvent> Appended, long LastPosition);
