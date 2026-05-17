using Memoria.Results;

namespace Memoria.EventSourcing.Dcb;

/// <summary>
/// Read/write surface for a Dynamic Consistency Boundary event store.
/// Consistency is per-query, not per-stream: the caller queries a slice of the log,
/// makes a decision, and appends with a token that captures "I read up to position X".
/// </summary>
public interface IDcbStore
{
    /// <summary>
    /// Returns all events matching <paramref name="query"/> in <c>GlobalPosition</c> order, oldest first.
    /// </summary>
    Task<Result<IReadOnlyList<StoredEvent>>> Query(
        DcbQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends events without a concurrency check. Use for events that carry no invariant.
    /// </summary>
    Task<Result<AppendResult>> Append(
        IReadOnlyList<EventToAppend> events,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends events atomically iff no event matching <paramref name="condition"/>'s query
    /// exists with <c>GlobalPosition &gt; condition.AfterPosition</c>. Returns
    /// <see cref="ConcurrencyConflict"/> on violation.
    /// </summary>
    Task<Result<AppendResult>> Append(
        IReadOnlyList<EventToAppend> events,
        AppendCondition condition,
        CancellationToken cancellationToken = default);
}
