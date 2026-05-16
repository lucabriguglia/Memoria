---
redirect_from:
  - /Domain-Service.html
  - /Domain-Service/
---

# Domain Service

The `IDomainService` interface provides a high-level API for managing aggregates and domain events in an event-sourced system. It abstracts the complexities of event storage, retrieval, and aggregate reconstruction, allowing developers to focus on business logic.

Every store provider has its own implementation of the `IDomainService` interface. You can use it by injecting the interface into your handlers, services, or controllers.

## Available Methods

- [Save Aggregate](#save-aggregate)
- [Save Domain Events](#save-domain-events)
- [Update Aggregate](#update-aggregate)
- [Get Aggregate](#get-aggregate)
- [Get In-Memory Aggregate](#get-in-memory-aggregate)
- [Get Events](#get-domain-events)
- [Get Events From Sequence](#get-domain-events-from-sequence)
- [Get Events Up To Sequence](#get-domain-events-up-to-sequence)
- [Get Events Between Sequences](#get-domain-events-between-sequences)
- [Get Events From Date](#get-domain-events-from-date)
- [Get Events Up To Date](#get-domain-events-up-to-date)
- [Get Events Between Dates](#get-domain-events-between-dates)
- [Get Events Applied To Aggregate](#get-domain-events-applied-to-aggregate)
- [Get Latest Event Sequence](#get-latest-event-sequence)

<a name="save-aggregate"></a>
### Save Aggregate
Saves an aggregate to the event store with optimistic concurrency control, persisting all uncommitted domain events and updating the aggregate snapshot.

**New aggregate**
```C#
var streamId = new CustomerStreamId(customerId);
var aggregateId = new OrderAggregateId(orderId);
var aggregate = new OrderAggregate(orderId, amount: 25.45m);

var saveAggregateResult = await domainService.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0);
```

**Update existing aggregate**
```C#
var streamId = new CustomerStreamId(customerId);
var aggregateId = new OrderAggregateId(orderId);
var latestEventSequence = await domainService.GetLatestEventSequence(streamId);

var aggregateResult = await domainService.GetAggregate(streamId, aggregateId);
if (!aggregateResult.IsSuccess)
{
    return aggregateResult.Error;
}
aggregate = aggregateResult.Value;

aggregate.UpdateAmount(amount: 15.00m);

var saveAggregateResult = await domainService.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: latestEventSequence);
```

<a name="save-domain-events"></a>
### Save Domain Events
Saves an array of domain events to the event store with optimistic concurrency control, bypassing aggregate persistence. This method is ideal for scenarios where events are generated outside traditional aggregate workflows.
```C#
var streamId = new CustomerStreamId(customerId);
var latestEventSequence = await domainService.GetLatestEventSequence(streamId);

var events = new @event[]
{
    new OrderPlaced
    {
        OrderId = orderId,
        Amount = 25.45m
    },
    new OrderShipped
    {
        OrderId = orderId,
        ShippedDate = _timeProvider.GetUtcNow()
    }
};
var saveEventsResult = await domainService.SaveEvents(streamId, events, expectedEventSequence: latestEventSequence);
```

<a name="update-aggregate"></a>
### Update Aggregate
Updates an aggregate with new events from its stream, applying any events that occurred after the aggregate's last known state.
If the aggregate does not exist, a new one is stored.
```C#
var streamId = new CustomerStreamId(customerId);
var aggregateId = new OrderAggregateId(orderId);
var updateAggregateResult = await domainService.UpdateAggregate(streamId, aggregateId);
```

<a name="get-aggregate"></a>
### Get Aggregate
Retrieves an aggregate from the event store, either from its snapshot or by reconstructing it from events.

**Read mode**:
- **SnapshotOnly** _(default)_: Retrieves the aggregate from its snapshot only. If no snapshot exists, returns null.
- **SnapshotWithNewEvents**: Retrieves the aggregate from its snapshot if it exists and applies any new events that have occurred since the snapshot. If no snapshot exists, returns null.
- **SnapshotOrCreate**: Retrieves the aggregate from its snapshot if it exists; otherwise, reconstructs it from events. If no events exist, returns null.
- **SnapshotWithNewEventsOrCreate**: Retrieves the aggregate from its snapshot if it exists, applies any new events that have occurred since the snapshot, or reconstructs it from events if no snapshot exists. If no events exist, returns null.

If the aggregate does not exist, but domain events that can be applied to the aggregate exist, the aggregate snapshot is stored automatically if read mode is SnapshotOrCreate or SnapshotWithNewEventOrCreate. This is useful when the domain changes, and you need a different aggregate structure. Increase the version of the aggregate type to force a snapshot creation.
```C#
var streamId = new CustomerStreamId(customerId);
var aggregateId = new OrderAggregateId(orderId);
var aggregateResult = await domainService.GetAggregate(streamId, aggregateId, ReadMode.SnapshotOrCreate);
```
If the aggregate does not exist and read mode is SnapshotOnly (default), the method returns null even if events that can be applied to the aggregate exist.
```C#
var streamId = new CustomerStreamId(customerId);
var aggregateId = new OrderAggregateId(orderId);
var aggregateResult = await domainService.GetAggregate(streamId, aggregateId);
```

<a name="get-in-memory-aggregate"></a>
### Get In-Memory Aggregate
Reconstructs an aggregate entirely from events without using snapshots, providing a pure event-sourced view of the aggregate state.
```C#
var streamId = new CustomerStreamId(customerId);
var aggregateId = new OrderAggregateId(orderId);
var aggregateResult = await domainService.GetInMemoryAggregate(streamId, aggregateId);
```
Optionally, you can specify a sequence number or a date to reconstruct the aggregate up to a specific point in time.
```C#
var aggregateResult = await domainService.GetInMemoryAggregate(streamId, aggregateId, upToSequence);
```
or
```C#
var aggregateResult = await domainService.GetInMemoryAggregate(streamId, aggregateId, upToDate);
```

<a name="get-domain-events"></a>
### Get Events
Retrieves all domain events from a specified stream, with optional filtering by event types and/or event properties.
```C#
var streamId = new CustomerStreamId(customerId);
var eventsResult = await domainService.GetEvents(streamId);
```
Optionally, you can filter the events by specific event types.
```C#
var streamId = new CustomerStreamId(customerId);
var eventTypes = new Type[] { typeof(OrderPlaced), typeof(OrderShipped) };
var eventsResult = await domainService.GetEvents(streamId, eventTypes);
```
Optionally, you can also filter the events by specific event properties (key/value pairs). All entries in the dictionary must match for an event to be included. Property and type filters can be combined.
```C#
var streamId = new CustomerStreamId(customerId);
var eventTypes = new Type[] { typeof(OrderPlaced), typeof(OrderShipped) };
var eventProperties = new Dictionary<string, string> { ["OrderId"] = orderId.ToString() };
var eventsResult = await domainService.GetEvents(streamId, eventTypes, eventProperties);
```

<a name="get-domain-events-from-sequence"></a>
### Get Events From Sequence
Retrieves domain events from a specified stream starting from a specific sequence number onwards, with optional filtering by event types and/or event properties.
```C#
var streamId = new CustomerStreamId(customerId);
var fromSequence = 5;
var eventsResult = await domainService.GetEventsFromSequence(streamId, fromSequence);
```
Optionally, you can filter the events by specific event types.
```C#
var streamId = new CustomerStreamId(customerId);
var fromSequence = 5;
var eventTypes = new Type[] { typeof(OrderPlaced), typeof(OrderShipped) };
var eventsResult = await domainService.GetEventsFromSequence(streamId, fromSequence, eventTypes);
```
Optionally, you can also filter the events by specific event properties.
```C#
var streamId = new CustomerStreamId(customerId);
var fromSequence = 5;
var eventTypes = new Type[] { typeof(OrderPlaced), typeof(OrderShipped) };
var eventProperties = new Dictionary<string, string> { ["OrderId"] = orderId.ToString() };
var eventsResult = await domainService.GetEventsFromSequence(streamId, fromSequence, eventTypes, eventProperties);
```

<a name="get-domain-events-up-to-sequence"></a>
### Get Events Up To Sequence
Retrieves domain events from a specified stream up to and including a specific sequence number, with optional filtering by event types and/or event properties.
```C#
var streamId = new CustomerStreamId(customerId);
var upToSequence = 10;
var eventsResult = await domainService.GetEventsUpToSequence(streamId, upToSequence);
```
Optionally, you can filter the events by specific event types.
```C#
var streamId = new CustomerStreamId(customerId);
var upToSequence = 10;
var eventTypes = new Type[] { typeof(OrderPlaced), typeof(OrderShipped) };
var eventsResult = await domainService.GetEventsUpToSequence(streamId, upToSequence, eventTypes);
```
Optionally, you can also filter the events by specific event properties.
```C#
var streamId = new CustomerStreamId(customerId);
var upToSequence = 10;
var eventTypes = new Type[] { typeof(OrderPlaced), typeof(OrderShipped) };
var eventProperties = new Dictionary<string, string> { ["OrderId"] = orderId.ToString() };
var eventsResult = await domainService.GetEventsUpToSequence(streamId, upToSequence, eventTypes, eventProperties);
```

<a name="get-domain-events-between-sequences"></a>
### Get Events Between Sequences
Retrieves domain events from a specified stream from and to specific sequence numbers, with optional filtering by event types and/or event properties.
```C#
var streamId = new CustomerStreamId(customerId);
var fromSequence = 5;
var toSequence = 10;
var eventsResult = await domainService.GetEventsBetweenSequences(streamId, fromSequence, toSequence);
```
Optionally, you can filter the events by specific event types.
```C#
var streamId = new CustomerStreamId(customerId);
var fromSequence = 5;
var toSequence = 10;
var eventTypes = new Type[] { typeof(OrderPlaced), typeof(OrderShipped) };
var eventsResult = await domainService.GetEventsBetweenSequences(streamId, fromSequence, toSequence, eventTypes);
```
Optionally, you can also filter the events by specific event properties.
```C#
var streamId = new CustomerStreamId(customerId);
var fromSequence = 5;
var toSequence = 10;
var eventTypes = new Type[] { typeof(OrderPlaced), typeof(OrderShipped) };
var eventProperties = new Dictionary<string, string> { ["OrderId"] = orderId.ToString() };
var eventsResult = await domainService.GetEventsBetweenSequences(streamId, fromSequence, toSequence, eventTypes, eventProperties);
```

<a name="get-domain-events-from-date"></a>
### Get Events From Date
Retrieves domain events from a specified stream starting from a specific date onwards, with optional filtering by event types and/or event properties.
```C#
var streamId = new CustomerStreamId(customerId);
var fromDate = new DateTime(2024, 6, 15, 17, 45, 48);
var eventsResult = await domainService.GetEventsFromDate(streamId, fromDate);
```
Optionally, you can filter the events by specific event types.
```C#
var streamId = new CustomerStreamId(customerId);
var fromDate = new DateTime(2024, 6, 15, 17, 45, 48);
var eventTypes = new Type[] { typeof(OrderPlaced), typeof(OrderShipped) };
var eventsResult = await domainService.GetEventsFromDate(streamId, fromDate, eventTypes);
```
Optionally, you can also filter the events by specific event properties.
```C#
var streamId = new CustomerStreamId(customerId);
var fromDate = new DateTime(2024, 6, 15, 17, 45, 48);
var eventTypes = new Type[] { typeof(OrderPlaced), typeof(OrderShipped) };
var eventProperties = new Dictionary<string, string> { ["OrderId"] = orderId.ToString() };
var eventsResult = await domainService.GetEventsFromDate(streamId, fromDate, eventTypes, eventProperties);
```

<a name="get-domain-events-up-to-date"></a>
### Get Events Up To Date
Retrieves domain events from a specified stream up to and including a specific date, with optional filtering by event types and/or event properties.
```C#
var streamId = new CustomerStreamId(customerId);
var upToDate = new DateTime(2024, 6, 15, 17, 45, 48);
var eventsResult = await domainService.GetEventsUpToDate(streamId, upToDate);
```
Optionally, you can filter the events by specific event types.
```C#
var streamId = new CustomerStreamId(customerId);
var upToDate = new DateTime(2024, 6, 15, 17, 45, 48);
var eventTypes = new Type[] { typeof(OrderPlaced), typeof(OrderShipped) };
var eventsResult = await domainService.GetEventsUpToDate(streamId, upToDate, eventTypes);
```
Optionally, you can also filter the events by specific event properties.
```C#
var streamId = new CustomerStreamId(customerId);
var upToDate = new DateTime(2024, 6, 15, 17, 45, 48);
var eventTypes = new Type[] { typeof(OrderPlaced), typeof(OrderShipped) };
var eventProperties = new Dictionary<string, string> { ["OrderId"] = orderId.ToString() };
var eventsResult = await domainService.GetEventsUpToDate(streamId, upToDate, eventTypes, eventProperties);
```

<a name="get-domain-events-between-dates"></a>
### Get Events Between Dates
Retrieves domain events from a specified stream from and to specific dates, with optional filtering by event types and/or event properties.
```C#
var streamId = new CustomerStreamId(customerId);
var fromDate = new DateTime(2024, 6, 15, 17, 45, 48);
var toDate = new DateTime(2024, 6, 25, 12, 46, 22);
var eventsResult = await domainService.GetEventsBetweenDates(streamId, fromDate, toDate);
```
Optionally, you can filter the events by specific event types.
```C#
var streamId = new CustomerStreamId(customerId);
var fromDate = new DateTime(2024, 6, 15, 17, 45, 48);
var toDate = new DateTime(2024, 6, 25, 12, 46, 22);
var eventTypes = new Type[] { typeof(OrderPlaced), typeof(OrderShipped) };
var eventsResult = await domainService.GetEventsBetweenDates(streamId, fromDate, toDate, eventTypes);
```
Optionally, you can also filter the events by specific event properties.
```C#
var streamId = new CustomerStreamId(customerId);
var fromDate = new DateTime(2024, 6, 15, 17, 45, 48);
var toDate = new DateTime(2024, 6, 25, 12, 46, 22);
var eventTypes = new Type[] { typeof(OrderPlaced), typeof(OrderShipped) };
var eventProperties = new Dictionary<string, string> { ["OrderId"] = orderId.ToString() };
var eventsResult = await domainService.GetEventsBetweenDates(streamId, fromDate, toDate, eventTypes, eventProperties);
```

<a name="get-domain-events-applied-to-aggregate"></a>
### Get Events Applied To Aggregate
Retrieves all domain events that have been applied to a specific aggregate instance, using the explicit aggregate-event relationship tracking. This method provides precise access to the events that actually contributed to an aggregate's current state.
```C#
var streamId = new CustomerStreamId(customerId);
var aggregateId = new OrderAggregateId(orderId);
var eventsResult = await domainService.GetEventsAppliedToAggregate(streamId, aggregateId);
```

<a name="get-latest-event-sequence"></a>
### Get Latest Event Sequence
Retrieves the latest event sequence number for a specified stream, with optional filtering by event types and/or event properties. This method provides the current position in an event stream, essential for optimistic concurrency control and determining where to append new events in event sourcing operations.
```C#
var streamId = new CustomerStreamId(customerId);
var latestEventSequence = await domainService.GetLatestEventSequence(streamId);
```
Optionally, you can filter the events by specific event types.
```C#
var streamId = new CustomerStreamId(customerId);
var eventTypes = new Type[] { typeof(OrderPlaced), typeof(OrderShipped) };
var latestEventSequence = await domainService.GetLatestEventSequence(streamId, eventTypes);
```
Optionally, you can also filter the events by specific event properties.
```C#
var streamId = new CustomerStreamId(customerId);
var eventTypes = new Type[] { typeof(OrderPlaced), typeof(OrderShipped) };
var eventProperties = new Dictionary<string, string> { ["OrderId"] = orderId.ToString() };
var latestEventSequence = await domainService.GetLatestEventSequence(streamId, eventTypes, eventProperties);
```
