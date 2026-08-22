# Projections

A **projection** is a read model: a query-optimised view of state that is built by applying domain events, then stored so it can be read back quickly. Where an [aggregate](aggregates-and-streams.md) is the *write* model — it produces new events and enforces invariants — a projection only *reads* events and never produces them.

Both aggregates and projections share the same event-application machinery through a common base class, `EventSourcedModel` (identity, `Version`, `EventTypeFilter`, and `Apply`). `AggregateRoot` adds the write-side concerns (`Add`, `UncommittedEvents`); `Projection` adds nothing — it is deliberately smaller.

- [Projection](#projection) — the read model
- [Projection Id](#projection-id) — identifies a projection snapshot
- [Saving and retrieving](#saving-and-retrieving) — snapshot persistence
- [How projections are stored](#how-projections-are-stored)

<a name="projection"></a>
## Projection

A projection inherits from `Projection` and, like an aggregate, declares an `EventTypeFilter` and an `Apply` method. It has no `Add` method and no uncommitted events, because it never creates events.

```C#
[ProjectionType("OrderSummary")]
public class OrderSummary : Projection
{
    public override Type[] EventTypeFilter { get; } =
    [
        typeof(OrderPlaced),
        typeof(OrderShipped)
    ];

    public int OrderCount { get; private set; }
    public decimal TotalAmount { get; private set; }

    protected override bool Apply<T>(T @event)
    {
        return @event switch
        {
            OrderPlaced placed => Apply(placed),
            OrderShipped shipped => Apply(shipped),
            _ => false
        };
    }

    private bool Apply(OrderPlaced @event)
    {
        OrderCount++;
        TotalAmount += @event.Amount;

        return true;
    }

    private bool Apply(OrderShipped @event) => true;
}
```

The `[ProjectionType("Name", version)]` attribute gives the projection a stable, versioned logical name used when its snapshot is serialized and deserialized — the projection equivalent of `[AggregateType]`. Projection types are discovered and registered automatically when you call `AddMemoriaEventSourcing(...)`, so no manual type-binding is required.

<a name="projection-id"></a>
## Projection Id

A Projection Id uniquely identifies a projection snapshot and serves as its persistence key. It implements `IProjectionId<TProjection>`.

```C#
public class OrderSummaryProjectionId(string customerId) : IProjectionId<OrderSummary>
{
    public string Id => $"order-summary:{customerId}";
}

var projectionId = new OrderSummaryProjectionId(customerId);
```

<a name="saving-and-retrieving"></a>
## Saving and retrieving

You build a projection by applying the events you care about, then persist it with `SaveProjection`. Read it back later with `GetProjection`.

```C#
var streamId = new CustomerStreamId(customerId);
var projectionId = new OrderSummaryProjectionId(customerId);

// Build the read model from the stream's events...
var eventsResult = await domainService.GetEvents(streamId);
var projection = new OrderSummary();
projection.Apply(eventsResult.Value);

// ...persist it as a snapshot...
await domainService.SaveProjection(streamId, projectionId, projection);

// ...and read it back.
var projectionResult = await domainService.GetProjection(streamId, projectionId);
```

`GetProjection` returns `null` when no snapshot has been saved for the projection id.

See the [Domain Service](../reference/domain-service.md#save-projection) reference for the full method signatures.

<a name="how-projections-are-stored"></a>
## How projections are stored

A projection is persisted and read like an [aggregate snapshot](../concepts/glossary.md#snapshot): its state is serialized into a snapshot record. Because a projection produces no events, saving one upserts only the snapshot — no events are written to the stream. Each store uses a **dedicated projection type**, separate from aggregate storage:

- **Entity Framework Core** persists projections through a `ProjectionEntity` mapped to its own `DomainProjections` table (distinct from the `DomainAggregates` table).
- **Cosmos DB** persists projections as a `ProjectionDocument` (`documentType: "Projection"`) in the **same container** as aggregates — Cosmos containers are schemaless and discriminate by document type.

A few things to keep in mind for this release:

- **Keyed by `{projectionId}:{version}`.** The projection's `[ProjectionType]` version is part of the snapshot key, so bumping the version starts a fresh snapshot rather than overwriting the previous one.
- **No optimistic concurrency.** Unlike `SaveAggregate` (which takes an `expectedEventSequence`), `SaveProjection` is a plain upsert. Coordinate writers yourself if more than one process rebuilds the same projection.

## Related

- [Aggregates and Streams](aggregates-and-streams.md)
- [Domain Service](../reference/domain-service.md)
- [Glossary](glossary.md)
