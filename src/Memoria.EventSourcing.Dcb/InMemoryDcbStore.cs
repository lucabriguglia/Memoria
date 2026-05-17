using Memoria.Results;

namespace Memoria.EventSourcing.Dcb;

/// <summary>
/// Thread-safe in-memory <see cref="IDcbStore"/>. Intended for tests and as the
/// default registration when no real adapter is configured.
/// </summary>
public sealed class InMemoryDcbStore : IDcbStore
{
    private readonly object _lock = new();
    private readonly List<StoredEvent> _events = [];

    public Task<Result<IReadOnlyList<StoredEvent>>> Query(DcbQuery query, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            IReadOnlyList<StoredEvent> matching = _events
                .Where(e => query.Matches(e.Payload.GetType(), e.Tags))
                .ToList();
            return Task.FromResult<Result<IReadOnlyList<StoredEvent>>>(Result<IReadOnlyList<StoredEvent>>.Ok(matching));
        }
    }

    public Task<Result<AppendResult>> Append(IReadOnlyList<EventToAppend> events, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(AppendUnlocked(events));
        }
    }

    public Task<Result<AppendResult>> Append(IReadOnlyList<EventToAppend> events, AppendCondition condition, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            foreach (var stored in _events)
            {
                if (stored.GlobalPosition <= condition.AfterPosition) continue;
                if (condition.Query.Matches(stored.Payload.GetType(), stored.Tags))
                {
                    return Task.FromResult<Result<AppendResult>>(
                        new ConcurrencyConflict(condition.Query, condition.AfterPosition, stored.GlobalPosition));
                }
            }
            return Task.FromResult(AppendUnlocked(events));
        }
    }

    private Result<AppendResult> AppendUnlocked(IReadOnlyList<EventToAppend> events)
    {
        var appended = new List<StoredEvent>(events.Count);
        var now = DateTimeOffset.UtcNow;
        foreach (var toAppend in events)
        {
            var position = _events.Count + 1L;
            var stored = new StoredEvent(
                EventId: Guid.NewGuid().ToString(),
                GlobalPosition: position,
                Payload: toAppend.Payload,
                Tags: toAppend.Tags,
                RecordedAt: now);
            _events.Add(stored);
            appended.Add(stored);
        }
        var lastPosition = appended.Count == 0 ? 0L : appended[^1].GlobalPosition;
        return Result<AppendResult>.Ok(new AppendResult(appended, lastPosition));
    }
}
