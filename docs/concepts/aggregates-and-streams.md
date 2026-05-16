---
redirect_from:
  - /Domain.html
  - /Domain/
---

# Aggregates and Streams

Memoria models state as a stream of immutable domain events. An **aggregate** is a consistency boundary that derives its state by applying the events in a **stream** it belongs to. Each piece below is a separately-addressable identity in code — pick the right one and the framework does the rest.

- [Stream Id](#stream-id) — identifies the event stream
- [Domain Events](#domain-events) — the facts in the stream
- [Aggregate Id](#aggregate-id) — identifies an aggregate within a stream
- [Aggregate](#aggregate) — the reconstructed state

<a name="stream-id"></a>
## Stream Id

A Stream Id is a unique identifier that represents a specific event stream. For example, a stream could represent all events related to a specific customer or order.

```C#
public class CustomerStreamId(string customerId) : IStreamId
{
    public string Id => $"customer:{customerId}";
}

var streamId = new CustomerStreamId(customerId);
```

<a name="domain-events"></a>
## Domain Events

Domain events represent business decisions that have happened in the domain and are stored as part of an event stream.

```C#
[EventType("OrderPlaced")]
public record OrderPlacedEvent(Guid orderId, decimal amount) : IEvent;
```

<a name="aggregate-id"></a>
## Aggregate Id

An Aggregate Id uniquely identifies aggregate instances within the domain and serves as the primary key for aggregate persistence and retrieval.

```C#
public class OrderAggregateId(string orderId) : IAggregateId<OrderAggregate>
{
    public string Id => $"order:{orderId}";

    public IDictionary<string, string>? EventPropertyFilter { get; } = null;
}

var aggregateId = new OrderAggregateId(orderId);
```

### Event Property Filter

In addition to the aggregate's `EventTypeFilter`, the aggregate id can declare an optional `EventPropertyFilter` made of key/value pairs. When the aggregate is retrieved or reconstructed in memory, only the events whose properties match every entry in the filter will be applied.

This is particularly useful when multiple aggregate instances share the same stream and the same event types, and need to be told apart by the value of one or more event properties (for example, a specific order id, a tenant id, or a region).

```C#
public class OrderAggregateId(Guid orderId) : IAggregateId<OrderAggregate>
{
    public string Id => $"order:{orderId}";

    public IDictionary<string, string>? EventPropertyFilter { get; } = new Dictionary<string, string>
    {
        ["OrderId"] = orderId.ToString()
    };
}
```

<a name="aggregate"></a>
## Aggregate

Aggregates are consistency boundaries that encapsulate business logic and maintain invariants through the application of domain events stored in event streams. 

Domain events in an event stream can be handled by multiple aggregates, but each aggregate instance is responsible for applying only the events that pertain to it.

Aggregates have an event type filter that specifies which types of events they can handle. When loading an aggregate from an event stream, only the events that match the aggregate's event type filter are applied to reconstruct its state. If no events are specified in the filter, the aggregate will load all events from the stream.

```C#
[AggregateType("Order")]
public class Order : AggregateRoot
{
    public override Type[] EventTypeFilter { get; } =
    [
        typeof(OrderPlaced)
    ];
        
    public Guid OrderId { get; private set; }
    public decimal Amount { get; private set; }

    public Order() { }

    public Order(Guid orderId, decimal amount)
    {
        Add(new OrderPlaced
        {
            OrderId = orderId,
            Amount = amount
        });
    }

    protected override bool Apply<T>(T @event)
    {
        return @event switch
        {
            OrderPlaced @event => Apply(@event),
            _ => false
        };
    }

    private bool Apply(OrderPlaced @event)
    {
        OrderId = @event.OrderId;
        Amount = @event.amount;

        return true;
    }
}
```
