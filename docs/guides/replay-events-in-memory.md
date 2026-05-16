# Replay events in memory

Sometimes you need an aggregate's state without writing a snapshot — for audit, debug, "what did this look like on day X", or rebuilding state after a model change. `IDomainService.GetInMemoryAggregate` reconstructs the aggregate purely from its events, with no snapshot involved.

This is read-only: nothing is written to the store.

## Reconstruct the current state

```C#
var streamId = new CustomerStreamId(customerId);
var aggregateId = new OrderAggregateId(orderId);

var aggregate = await domainService.GetInMemoryAggregate(streamId, aggregateId);
```

Every event in the stream that matches the aggregate's `EventTypeFilter` (and `EventPropertyFilter`, if any) is applied in sequence.

## Reconstruct up to a sequence number

Useful for debugging — "what did this aggregate look like right before the bad event landed?":

```C#
var aggregate = await domainService.GetInMemoryAggregate(
    streamId, aggregateId, upToSequence: 42);
```

## Reconstruct as of a timestamp

Useful for audit reports and historical queries:

```C#
var aggregate = await domainService.GetInMemoryAggregate(
    streamId, aggregateId, upToDate: someDate);
```

## When to use this vs `GetAggregate`

| Question                                                            | Use                                            |
|---------------------------------------------------------------------|------------------------------------------------|
| I want the latest state for normal application reads                | `GetAggregate` with the right [read mode](../concepts/read-modes.md) |
| I want the state at a past point in time                            | `GetInMemoryAggregate(upToSequence \| upToDate)` |
| I want to inspect history without affecting the snapshot            | `GetInMemoryAggregate`                         |
| I just changed the aggregate's structure and need to rebuild        | `GetAggregate` with `SnapshotOrCreate` and a bumped aggregate version |

## Related

- [Domain Service](../reference/domain-service.md)
- [Read Modes](../concepts/read-modes.md)
