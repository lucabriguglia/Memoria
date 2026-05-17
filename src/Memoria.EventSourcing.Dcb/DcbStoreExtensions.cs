using Memoria.EventSourcing.Domain;
using Memoria.Results;

namespace Memoria.EventSourcing.Dcb;

/// <summary>
/// Caller ergonomics for <see cref="IDcbStore"/>.
/// </summary>
public static class DcbStoreExtensions
{
    /// <summary>
    /// Runs the canonical DCB read-decide-append loop in one call:
    /// query the slice, fold events into a decision state, run the decision,
    /// and append the emitted events with a concurrency token derived from the
    /// position the read observed.
    /// </summary>
    /// <param name="store">The DCB store.</param>
    /// <param name="query">The slice of the log the decision depends on.</param>
    /// <param name="initialState">Starting state before any events are folded in.</param>
    /// <param name="fold">Pure reducer that applies one event to the running state.</param>
    /// <param name="decide">
    /// Pure decision that, given the final folded state, returns either the events
    /// to append, an empty list (valid but no change), or a <see cref="Failure"/>
    /// to reject the operation.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task<Result<AppendResult>> Decide<TState>(
        this IDcbStore store,
        DcbQuery query,
        TState initialState,
        Func<TState, IEvent, TState> fold,
        Func<TState, Result<IReadOnlyList<EventToAppend>>> decide,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(fold);
        ArgumentNullException.ThrowIfNull(decide);

        var read = await store.Query(query, cancellationToken);
        if (read.IsNotSuccess) return read.Failure!;

        var events = read.Value!;
        var state = initialState;
        foreach (var stored in events)
        {
            state = fold(state, stored.Payload);
        }

        var capturedPosition = events.Count == 0 ? 0L : events[^1].GlobalPosition;

        var decision = decide(state);
        if (decision.IsNotSuccess) return decision.Failure!;

        var toAppend = decision.Value!;
        if (toAppend.Count == 0)
        {
            return Result<AppendResult>.Ok(new AppendResult(Array.Empty<StoredEvent>(), capturedPosition));
        }

        return await store.Append(toAppend, new AppendCondition(query, capturedPosition), cancellationToken);
    }
}
