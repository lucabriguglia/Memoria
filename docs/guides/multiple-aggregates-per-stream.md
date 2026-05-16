---
redirect_from:
  - /Event-Sourcing-Scenarios.html
  - /Event-Sourcing-Scenarios/
  - /Entity-Framework-Core-Scenarios.html
  - /Entity-Framework-Core-Scenarios/
---

# Multiple aggregates per stream

In Memoria, an event stream can hold events for **more than one aggregate**. Each aggregate decides which events it cares about via its `EventTypeFilter` and (optionally) its `EventPropertyFilter`. This is how you model related-but-distinct concerns in one place — for example, a customer's order, a customer's loyalty balance, and a customer's preferences all written to the same `customer:{id}` stream.

This guide covers two patterns:

1. **Disambiguating by event type only** — each aggregate handles a different set of event types.
2. **Disambiguating by event properties** — multiple aggregates handle the same event types and distinguish themselves by a value on each event.

## 1. Disambiguating by event type

Each aggregate's `EventTypeFilter` lists the events it applies. When loading an aggregate, only matching events are replayed.

```C#
[AggregateType("Order")]
public class Order : AggregateRoot
{
    public override Type[] EventTypeFilter { get; } = [typeof(OrderPlaced), typeof(OrderShipped)];
    // …
}

[AggregateType("Loyalty")]
public class Loyalty : AggregateRoot
{
    public override Type[] EventTypeFilter { get; } = [typeof(PointsAwarded), typeof(PointsRedeemed)];
    // …
}
```

Both can live on `customer:{id}` without interfering. Saving one updates the stream; loading the other replays only its own events.

## 2. Disambiguating by event property

When two aggregate instances of the same type live on the same stream and apply the same event types — for example, two different orders on the same customer stream — you need a way to tell them apart. Declare an `EventPropertyFilter` on the aggregate id. The framework only applies events whose serialized properties match every entry.

```C#
public class OrderAggregateId(Guid orderId) : IAggregateId<Order>
{
    public string Id => $"order:{orderId}";

    public IDictionary<string, string>? EventPropertyFilter { get; } = new Dictionary<string, string>
    {
        ["OrderId"] = orderId.ToString()
    };
}
```

The filter combines with the aggregate's `EventTypeFilter`: an event must match both to be applied.

## Saving and loading

Patterns below work with either `IDomainService` or the Entity Framework Core `IDomainDbContext` extensions, which expose `TrackAggregate` / `TrackEventEntities` for combining event-sourced writes with other EF Core changes in a single transaction.

### Through `IDomainService`

```C#
var streamId = new CustomerStreamId(customerId);
var orderId = new OrderAggregateId(orderGuid);
var loyaltyId = new LoyaltyAggregateId(customerId);

var order = new Order(orderGuid, amount: 25.45m);

var saveResult = await domainService.SaveAggregate(
    streamId, orderId, order, expectedEventSequence: 0);

// Other aggregates pick up the events their filter matches.
var loyalty = await domainService.GetAggregate(
    streamId, loyaltyId, ReadMode.SnapshotWithNewEventsOrCreate);
```

### Through EF Core `TrackAggregate`

When you also need to write entities that aren't event-sourced in the same transaction:

```C#
var trackAggregateResult = await dbContext.TrackAggregate(
    streamId, orderId, order, expectedEventSequence: 0);

if (!trackAggregateResult.IsSuccess) return trackAggregateResult.Error;

await dbContext.TrackEventEntities(
    streamId, loyaltyId,
    trackAggregateResult.Value.EventEntities!,
    expectedEventSequence: 0);

// …additional change tracking…

await dbContext.Save();
```

`TrackEvents` is the lower-level alternative when you want to write raw events first, then attach them to one or more aggregates:

```C#
var trackEvents = await dbContext.TrackEvents(streamId, events, expectedEventSequence: 0);
var eventEntities = trackEvents.Value.EventEntities!;

await dbContext.TrackEventEntities(streamId, orderId, eventEntities, expectedEventSequence: 0);
await dbContext.TrackEventEntities(streamId, loyaltyId, eventEntities, expectedEventSequence: 0);

await dbContext.Save();
```

## Related

- [Aggregates and Streams](../concepts/aggregates-and-streams.md)
- [Read Modes](../concepts/read-modes.md)
- [Domain Service](../reference/domain-service.md)
- [EF Core Extensions](../reference/ef-core-extensions.md)
