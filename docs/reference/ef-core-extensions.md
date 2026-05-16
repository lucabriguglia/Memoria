---
redirect_from:
  - /Entity-Framework-Core-Extensions.html
  - /Entity-Framework-Core-Extensions/
---

# Entity Framework Core Extensions

The Entity Framework Core store provider offers a variety of built-in extension methods of the DbContext to facilitate interaction with aggregates and events. Since the store provider is based purely on the DbContext, it's extremily easy to create your own extensions to create any kind of reporting. Below is a categorized list of the built-in methods:

- [Saving](#saving)
  - [Save Aggregate](#save-aggregate)
  - [Save Domain Events](#save-domain-events)
  - [Save](#save)
  - [Update Aggregate](#update-aggregate)
- [Tracking](#tracking)
  - [Track Aggregate](#track-aggregate)
  - [Track Domain Events](#track-domain-events)
  - [Track Event Entities](#track-event-entities)
- [Retrieving Aggregates and Domain Events](#retrieving-aggregates-and-domain-events)
  - [Get Aggregate](#get-aggregate)
  - [Get In-Memory Aggregate](#get-in-memory-aggregate)
  - [Get Domain Events](#get-domain-events)
  - [Get Domain Events From Sequence](#get-domain-events-from-sequence)
  - [Get Domain Events Up To Sequence](#get-domain-events-up-to-sequence)
  - [Get Domain Events Between Sequences](#get-domain-events-between-sequences)
  - [Get Domain Events From Date](#get-domain-events-from-date)
  - [Get Domain Events Up To Date](#get-domain-events-up-to-date)
  - [Get Domain Events Between Dates](#get-domain-events-between-dates)
  - [Get Domain Events Applied To Aggregate](#get-domain-events-applied-to-aggregate)
  - [Get Latest Event Sequence](#get-latest-event-sequence)
- [Retrieving Database Entities](#retrieving-database-entities)
  - [Get Event Entities](#get-event-entities)
  - [Get Event Entities Between Sequences](#get-event-entities-between-sequences)
  - [Get Event Entities From Sequence](#get-event-entities-from-sequence)
  - [Get Event Entities Up To Sequence](#get-event-entities-up-to-sequence)
  - [Get Event Entities Applied To Aggregate](#get-event-entities-applied-to-aggregate)
  - [Get Aggregate Event Entities](#get-aggregate-event-entities)

<a name="saving"></a>
## Saving

<a name="save-aggregate"></a>
### Save Aggregate
Saves an aggregate to the event store with optimistic concurrency control, persisting all uncommitted domain events and updating the aggregate snapshot.

**New aggregate**
```C#
var streamId = new CustomerStreamId(customerId);
var aggregateId = new OrderAggregateId(orderId);
var aggregate = new OrderAggregate(orderId, amount: 25.45m);

var saveAggregateResult = await dbContext.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0);
```

**Update existing aggregate**
```C#
var streamId = new CustomerStreamId(customerId);
var aggregateId = new OrderAggregateId(orderId);
var latestEventSequence = await domainDbContext.GetLatestEventSequence(streamId);

var aggregateResult = await dbContext.GetAggregate(streamId, aggregateId);
if (!aggregateResult.IsSuccess)
{
    return aggregateResult.Error;
}
aggregate = aggregateResult.Value;

aggregate.UpdateAmount(amount: 15.00m);

var saveAggregateResult = await dbContext.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: latestEventSequence);
```

<a name="save-domain-events"></a>
### Save Domain Events
Saves an array of domain events to the event store with optimistic concurrency control, bypassing aggregate persistence. This method is ideal for scenarios where events are generated outside traditional aggregate workflows.
```C#
var streamId = new CustomerStreamId(customerId);
var latestEventSequence = await domainDbContext.GetLatestEventSequence(streamId);

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
var saveEventsResult = await dbContext.SaveEvents(streamId, events, expectedEventSequence: latestEventSequence);
```

<a name="save"></a>
### Save
Saves all pending changes in the domain database context to the underlying data store. This method provides a simple way to persist tracked entity changes without additional event sourcing logic, suitable for scenarios where entities have been explicitly tracked.
```C#
// ...track aggregates and domain events...

var item = new ItemEntity
{
    Id = Guid.NewGuid(),
    Name = "Sample Item",
    Price = 9.99m
};
dbContext.Items.Add(item);
var saveResult = await dbContext.Save();
```

<a name="update-aggregate"></a>
### Update Aggregate
Updates an existing aggregate with new events from its stream, applying any events that occurred after the aggregate's last known state.
```C#
var streamId = new CustomerStreamId(customerId);
var aggregateId = new OrderAggregateId(orderId);
var updateAggregateResult = await dbContext.UpdateAggregate(streamId, aggregateId);
```
<a name="tracking"></a>
## Tracking

<a name="track-aggregate"></a>
### Track Aggregate
Tracks an aggregate's uncommitted events and state changes in the Entity Framework change tracker without persisting to the database, preparing all necessary entities for subsequent save operations.
```C#
var streamId = new CustomerStreamId(customerId);
var aggregateId = new OrderAggregateId(orderId);
var latestEventSequence = await domainDbContext.GetLatestEventSequence(streamId);

var aggregateResult = await dbContext.GetAggregate(streamId, aggregateId);
if (!aggregateResult.IsSuccess)
{
    return aggregateResult.Error;
}
aggregate = aggregateResult.Value;

aggregate.UpdateAmount(amount: 15.00m);

await dbContext.TrackAggregate(streamId, aggregateId, aggregate, expectedEventSequence: latestEventSequence);

// ...additional entity changes...

var saveResult = await dbContext.Save();
```

<a name="track-domain-events"></a>
### Track Domain Events
Tracks an array of domain events in the Entity Framework change tracker without persisting to the database, preparing event entities for later save operations with proper sequencing and concurrency control validation.
```C#
var streamId = new CustomerStreamId(customerId);
var latestEventSequence = await domainDbContext.GetLatestEventSequence(streamId);

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
await dbContext.TrackEvents(streamId, events, expectedEventSequence: latestEventSequence);

// ...additional entity changes...

var saveResult = await dbContext.Save();
```

<a name="track-event-entities"></a>
### Track Event Entities
Tracks an aggregate's state changes based on a list of event entities, applying only events that the aggregate can handle and updating its snapshot accordingly.
```C#
var streamId = new CustomerStreamId(customerId);
var orderAggregateId = new OrderAggregateId(orderId);
var anotherAggregateId = new AnotherAggregateId(orderId);
var aggregate = new OrderAggregate(orderId, amount: 25.45m);

var trackAggregateResult = await dbContext.TrackAggregate(streamId, orderAggregateId, aggregate, expectedEventSequence: 0);
if (!trackAggregateResult.IsSuccess)
{
    return trackResult.Error;
}
// Track same event entities for a different aggregate
await dbContext.TrackEventEntities(streamId, anotherAggregateId, trackAggregateResult.Value.EventEntities!, expectedEventSequence: 0);

var saveResult = await dbContext.Save();
```
<a name="retrieving-aggregates-and-domain-events"></a>
## Retrieving Aggregates and Domain Events

<a name="get-aggregate"></a>
### Get Aggregate
Retrieves an aggregate from the event store, either from its snapshot or by reconstructing it from events.

If the aggregate does not exist, but domain events that can be applied to the aggregate exist, the aggregate snapshot is stored automatically. This is useful when the domain changes, and you need a different aggregate structure. Increase the version of the aggregate type to force a snapshot creation.

```C#
var streamId = new CustomerStreamId(customerId);
var aggregateId = new OrderAggregateId(orderId);
var aggregateResult = await dbContext.GetAggregate(streamId, aggregateId);
```

Optionally, it can be forced to apply any new domain events that occurred after the snapshot was created. This is useful when you want to ensure the aggregate is up to date with the latest events. If new events are found, the aggregate snapshot is updated automatically.
```C#
var streamId = new CustomerStreamId(customerId);
var aggregateId = new OrderAggregateId(orderId);
var aggregateResult = await dbContext.GetAggregate(streamId, aggregateId, ReadMode.SnapshotWithNewEvents);
```

<a name="get-in-memory-aggregate"></a>
### Get In-Memory Aggregate
Reconstructs an aggregate entirely from events without using snapshots, providing a pure event-sourced view of the aggregate state.
```C#
var streamId = new CustomerStreamId(customerId);
var aggregateId = new OrderAggregateId(orderId);
var aggregateResult = await dbContext.GetInMemoryAggregate(streamId, aggregateId);
```

<a name="get-domain-events"></a>
### Get Domain Events
Retrieves all domain events from a specified stream, with optional filtering by event types.
```C#
var streamId = new CustomerStreamId(customerId);
var eventsResult = await dbContext.GetEvents(streamId);
```
Optionally, you can filter the events by specific event types.
```C#
var streamId = new CustomerStreamId(customerId);
var eventTypes = new Type[] { typeof(OrderPlaced), typeof(OrderShipped) };
var eventsResult = await dbContext.GetEvents(streamId, eventTypes);
```

<a name="get-domain-events-from-sequence"></a>
### Get Domain Events From Sequence
Retrieves domain events from a specified stream starting from a specific sequence number onwards, with optional filtering by event types.
```C#
var streamId = new CustomerStreamId(customerId);
var fromSequence = 5;
var eventsResult = await dbContext.GetEventsFromSequence(streamId, fromSequence);
```
Optionally, you can filter the events by specific event types.
```C#
var streamId = new CustomerStreamId(customerId);
var fromSequence = 5;
var eventTypes = new Type[] { typeof(OrderPlaced), typeof(OrderShipped) };
var eventsResult = await dbContext.GetEventsFromSequence(streamId, fromSequence, eventTypes);
```

<a name="get-domain-events-up-to-sequence"></a>
### Get Domain Events Up To Sequence
Retrieves domain events from a specified stream up to and including a specific sequence number, with optional filtering by event types.
```C#
var streamId = new CustomerStreamId(customerId);
var upToSequence = 10;
var eventsResult = await dbContext.GetEventsUpToSequence(streamId, upToSequence);
```
Optionally, you can filter the events by specific event types.
```C#
var streamId = new CustomerStreamId(customerId);
var upToSequence = 10;
var eventTypes = new Type[] { typeof(OrderPlaced), typeof(OrderShipped) };
var eventsResult = await dbContext.GetEventsUpToSequence(streamId, upToSequence, eventTypes);
```

<a name="get-domain-events-between-sequences"></a>
### Get Domain Events Between Sequences
Retrieves domain events from a specified stream from and to specific sequence numbers, with optional filtering by event types.
```C#
var streamId = new CustomerStreamId(customerId);
var fromSequence = 5;
var toSequence = 10;
var eventsResult = await dbContext.GetEventsBetweenSequences(streamId, fromSequence, toSequence);
```
Optionally, you can filter the events by specific event types.
```C#
var streamId = new CustomerStreamId(customerId);
var fromSequence = 5;
var toSequence = 10;
var eventTypes = new Type[] { typeof(OrderPlaced), typeof(OrderShipped) };
var eventsResult = await dbContext.GetEventsBetweenSequences(streamId, fromSequence, toSequence, eventTypes);
```

<a name="get-domain-events-from-date"></a>
### Get Domain Events From Date
Retrieves domain events from a specified stream starting from a specific date onwards, with optional filtering by event types.
```C#
var streamId = new CustomerStreamId(customerId);
var fromDate = new DateTime(2024, 6, 15, 17, 45, 48);
var eventsResult = await dbContext.GetEventsFromDate(streamId, fromDate);
```
Optionally, you can filter the events by specific event types.
```C#
var streamId = new CustomerStreamId(customerId);
var fromDate = new DateTime(2024, 6, 15, 17, 45, 48);
var eventTypes = new Type[] { typeof(OrderPlaced), typeof(OrderShipped) };
var eventsResult = await dbContext.GetEventsFromDate(streamId, fromDate, eventTypes);
```

<a name="get-domain-events-up-to-date"></a>
### Get Domain Events Up To Date
Retrieves domain events from a specified stream up to and including a specific date, with optional filtering by event types.
```C#
var streamId = new CustomerStreamId(customerId);
var upToDate = new DateTime(2024, 6, 15, 17, 45, 48);
var eventsResult = await dbContext.GetEventsUpToDate(streamId, upToDate);
```
Optionally, you can filter the events by specific event types.
```C#
var streamId = new CustomerStreamId(customerId);
var upToDate = new DateTime(2024, 6, 15, 17, 45, 48);
var eventTypes = new Type[] { typeof(OrderPlaced), typeof(OrderShipped) };
var eventsResult = await dbContext.GetEventsUpToDate(streamId, upToDate, eventTypes);
```

<a name="get-domain-events-between-dates"></a>
### Get Domain Events Between Dates
Retrieves domain events from a specified stream from and to specific dates, with optional filtering by event types.
```C#
var streamId = new CustomerStreamId(customerId);
var fromDate = new DateTime(2024, 6, 15, 17, 45, 48);
var toDate = new DateTime(2024, 6, 25, 12, 46, 22);
var eventsResult = await dbContext.GetEventsBetweenDates(streamId, fromDate, toDate);
```
Optionally, you can filter the events by specific event types.
```C#
var streamId = new CustomerStreamId(customerId);
var fromDate = new DateTime(2024, 6, 15, 17, 45, 48);
var toDate = new DateTime(2024, 6, 25, 12, 46, 22);
var eventTypes = new Type[] { typeof(OrderPlaced), typeof(OrderShipped) };
var eventsResult = await dbContext.GetEventsBetweenDates(streamId, fromDate, toDate, eventTypes);
```

<a name="get-domain-events-applied-to-aggregate"></a>
### Get Domain Events Applied To Aggregate
Retrieves all domain events that have been applied to a specific aggregate instance, using the explicit aggregate-event relationship tracking. This method provides precise access to the events that actually contributed to an aggregate's current state.
```C#
var streamId = new CustomerStreamId(customerId);
var aggregateId = new OrderAggregateId(orderId);
var eventsResult = await dbContext.GetEventsAppliedToAggregate(streamId, aggregateId);
```

<a name="get-latest-event-sequence"></a>
### Get Latest Event Sequence
Retrieves the latest event sequence number for a specified stream, with optional filtering by event types. This method provides the current position in an event stream, essential for optimistic concurrency control and determining where to append new events in event sourcing operations.
```C#
var streamId = new CustomerStreamId(customerId);
var latestEventSequence = await dbContext.GetLatestEventSequence(streamId);
```
Optionally, you can filter the events by specific event types.
```C#
var streamId = new CustomerStreamId(customerId);
var eventTypes = new Type[] { typeof(OrderPlaced), typeof(OrderShipped) };
var latestEventSequence = await dbContext.GetLatestEventSequence(streamId, eventTypes);
```

<a name="retrieving-database-entities"></a>
## Retrieving Database Entities

<a name="get-event-entities"></a>
### Get Event Entities
Retrieves all event entities from a specified stream, with optional filtering by event types.
```C#
var streamId = new CustomerStreamId(customerId);
var eventEntitiesResult = await dbContext.GetEventEntities(streamId);
```
Optionally, you can filter the events by specific event types.
```C#
var streamId = new CustomerStreamId(customerId);
var eventTypes = new Type[] { typeof(OrderPlaced), typeof(OrderShipped) };
var eventEntitiesResult = await dbContext.GetEventEntities(streamId, eventTypes);
```

<a name="get-event-entities-between-sequences"></a>
### Get Event Entities Between Sequences
Retrieves event entities from a specified stream from and to specific sequence numbers, with optional filtering by event types.
```C#
var streamId = new CustomerStreamId(customerId);
var fromSequence = 5;
var toSequence = 10;
var eventsResult = await dbContext.EventEntitiesBetweenSequences(streamId, fromSequence, toSequence);
```
Optionally, you can filter the events by specific event types.
```C#
var streamId = new CustomerStreamId(customerId);
var fromSequence = 5;
var toSequence = 10;
var eventTypes = new Type[] { typeof(OrderPlaced), typeof(OrderShipped) };
var eventsResult = await dbContext.EventEntitiesBetweenSequences(streamId, fromSequence, toSequence, eventTypes);
```

<a name="get-event-entities-from-sequence"></a>
### Get Event Entities From Sequence
Retrieves a list of event entities from the specified stream starting from a given sequence number, with optional filtering by event types.
```C#
var streamId = new CustomerStreamId(customerId);
var fromSequence = 5;
var eventEntitiesResult = await dbContext.GetEventEntitiesFromSequence(streamId, fromSequence);
```
Optionally, you can filter the events by specific event types.
```C#
var streamId = new CustomerStreamId(customerId);
var fromSequence = 5;
var eventTypes = new Type[] { typeof(OrderPlaced), typeof(OrderShipped) };
var eventEntitiesResult = await dbContext.GetEventEntitiesFromSequence(streamId, fromSequence, eventTypes);
```

<a name="get-event-entities-up-to-sequence"></a>
### Get Event Entities Up To Sequence
Retrieves event entities from a specified stream up to and including a specific sequence number, with optional filtering by event types.
```C#
var streamId = new CustomerStreamId(customerId);
var upToSequence = 10;
var eventEntitiesResult = await dbContext.GetEventEntitiesUpToSequence(streamId, upToSequence);
```
Optionally, you can filter the events by specific event types.
```C#
var streamId = new CustomerStreamId(customerId);
var upToSequence = 10;
var eventTypes = new Type[] { typeof(OrderPlaced), typeof(OrderShipped) };
var eventEntitiesResult = await dbContext.GetEventEntitiesUpToSequence(streamId, upToSequence, eventTypes);
```

<a name="get-event-entities-applied-to-aggregate"></a>
### Get Event Entities Applied To Aggregate
Retrieves all event entities that have been applied to a specific aggregate instance, providing a complete audit trail of changes that contributed to the aggregate's current state.
```C#
var streamId = new CustomerStreamId(customerId);
var aggregateId = new OrderAggregateId(orderId);
var eventEntitiesResult = await dbContext.GetEventEntitiesAppliedToAggregate(streamId, aggregateId);
```

<a name="get-aggregate-event-entities"></a>
### Get Aggregate Event Entities
Retrieves all aggregate-event relationship entities associated with a specific aggregate instance, providing complete visibility into the many-to-many relationships between the aggregate and its applied events.
```C#
var streamId = new CustomerStreamId(customerId);
var aggregateId = new OrderAggregateId(orderId);
var aggregateEventEntitiesResult = await dbContext.GetAggregateEventEntities(streamId, aggregateId);
```
