# Quickstart: Event Sourcing

A ten-minute walk-through: persist an aggregate, reconstruct it from its events, and replay history in memory. Uses Entity Framework Core's in-memory provider so no database is required.

Prerequisites: [installed](install.md) the `Memoria`, `Memoria.EventSourcing`, and `Memoria.EventSourcing.Store.EntityFrameworkCore` packages.

## 1. Register event sourcing

```C#
public class AppDbContext(
    DbContextOptions<DomainDbContext> options,
    TimeProvider timeProvider,
    IHttpContextAccessor httpContextAccessor)
    : DomainDbContext(options, timeProvider, httpContextAccessor);

services
    .AddScoped(sp => new DbContextOptionsBuilder<DomainDbContext>()
        .UseInMemoryDatabase("memoria-quickstart")
        .UseApplicationServiceProvider(sp)
        .Options);

services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase("memoria-quickstart"));

services.AddMemoria(typeof(Program));
services.AddMemoriaEventSourcing(typeof(Program));
services.AddMemoriaEntityFrameworkCore<AppDbContext>();
```

For other store providers and the full configuration surface, see [Configuration: Event Sourcing](../reference/configuration/event-sourcing.md).

## 2. Model the domain

You need three things: an event, a stream id, an aggregate id, and an aggregate.

```C#
[EventType("OrderPlaced")]
public record OrderPlaced(Guid OrderId, decimal Amount) : IEvent;

public class CustomerStreamId(Guid customerId) : IStreamId
{
    public string Id => $"customer:{customerId}";
}

public class OrderAggregateId(Guid orderId) : IAggregateId<Order>
{
    public string Id => $"order:{orderId}";
    public IDictionary<string, string>? EventPropertyFilter => null;
}

[AggregateType("Order")]
public class Order : AggregateRoot
{
    public override Type[] EventTypeFilter { get; } = [typeof(OrderPlaced)];

    public Guid OrderId { get; private set; }
    public decimal Amount { get; private set; }

    public Order() { }

    public Order(Guid orderId, decimal amount)
    {
        Add(new OrderPlaced(orderId, amount));
    }

    protected override bool Apply<T>(T @event) => @event switch
    {
        OrderPlaced e => Apply(e),
        _ => false
    };

    private bool Apply(OrderPlaced e)
    {
        OrderId = e.OrderId;
        Amount = e.Amount;
        return true;
    }
}
```

The `EventTypeFilter` decides which events the aggregate applies — the same stream can hold events for many aggregates. See [Aggregates and Streams](../concepts/aggregates-and-streams.md) for the underlying model.

## 3. Save an aggregate

```C#
var customerId = Guid.NewGuid();
var orderId = Guid.NewGuid();

var streamId = new CustomerStreamId(customerId);
var aggregateId = new OrderAggregateId(orderId);
var order = new Order(orderId, amount: 25.45m);

var saveResult = await domainService.SaveAggregate(
    streamId,
    aggregateId,
    order,
    expectedEventSequence: 0); // optimistic concurrency
```

`SaveAggregate` writes the staged events **and** a snapshot in one transactional batch. `expectedEventSequence: 0` says "I expect this stream to be empty." If anyone else has written meanwhile, the call fails fast.

## 4. Load it back

```C#
var loaded = await domainService.GetAggregate(
    streamId,
    aggregateId,
    ReadMode.SnapshotOnly); // fastest path: one read
```

`SnapshotOnly` is the default. Pick a different [read mode](../concepts/read-modes.md) when you also need events appended since the snapshot, or when you want the aggregate reconstructed from events if no snapshot exists.

## 5. Replay history in memory

To inspect what the aggregate looked like at a point in time — for audit, debug, or what-if analysis — reconstruct it from events alone:

```C#
// Current state, rebuilt event-by-event
var current = await domainService.GetInMemoryAggregate(streamId, aggregateId);

// State after the first three events
var partial = await domainService.GetInMemoryAggregate(streamId, aggregateId, upToSequence: 3);

// State as of a given timestamp
var historical = await domainService.GetInMemoryAggregate(streamId, aggregateId, upToDate: someDate);
```

## Where to go next

- [Concepts: Read Modes](../concepts/read-modes.md) — pick the right one for your read pattern.
- [Concepts: Providers](../concepts/providers.md) — switch storage, plug in messaging or caching.
- [Reference: Domain Service](../reference/domain-service.md) — full API.
- For tests without external services, use the `*.InMemory` packages (Cosmos InMemory, EF Core's UseInMemoryDatabase, Service Bus InMemory, RabbitMQ InMemory).
