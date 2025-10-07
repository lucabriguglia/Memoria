# 🚀 OpenCQRS&trade;

[![.Build](https://github.com/OpenCQRS/OpenCQRS/actions/workflows/build.yml/badge.svg)](https://github.com/OpenCQRS/OpenCQRS/actions/workflows/build.yml)

.NET framework implementing DDD, Event Sourcing, and CQRS.

OpenCQRS 7 released in September 2025 is extremely flexible and expandable. It can be used as a simple mediator or as a full Event Sourcing solution with Cosmos DB or Entity Framework Core as storage.

- 📘 _[Full documentation](https://opencqrs.github.io/OpenCQRS/)_
- 📣 _[Release Notes](https://opencqrs.github.io/OpenCQRS/Release-Notes.html)_
- 📚 _[Examples in repository](https://github.com/OpenCQRS/OpenCQRS/tree/main/examples)_
- 🛒 _[EventShop (ecommerce demo application)](https://github.com/OpenCQRS/EventShop)_

## ⭐ Give a star

If you're using this repository for your learning, samples, workshop, or your project, please give a star. Thank you!

## ⚡Main Features

- Mediator with commands, queries, and notifications
- Multiple aggregates per stream
- Option to store the aggregate snapshot alongside events for fast reads, and write model strongly consistent
- Four different read modes that allow multiple write/read patterns based on specific needs.
- In memory aggregate reconstruction up to a specific event sequence or date if provided _**(soon up to aggregate version)**_
- Events applied to the aggregate filtered by event type
- Retrieval of all events applied to an aggregate
- Querying stream events from or up to a specific event sequence or date/date range
- Optimistic concurrency control with an expected event sequence
- Automatic event/notification publication after a command is successfully processed that returns a list of results from all notification handlers
- Automatic event/message publication after a command is successfully processed using Service Bus or RabbitMQ
- Automatic command validation with FluentValidation if required
- Command sequences that return a list of results from all commands in the sequence
- Custom command handlers or services can be used instead of the automatically resolved command handlers
- Result pattern across handlers and providers
- Extensible architecture with providers for store, messaging, caching, and validation

## 🗺️ Roadmap

### ⏳ In Progress
- New package for in-memory storage for easier testing in projects using OpenCQRS

### ⏭️ Next
- New package for in-memory RabbitMQ for easier testing in projects using OpenCQRS
- New package for in-memory Service Bus for easier testing in projects using OpenCQRS

### 🕙 To Follow
- Create an ecommerce demo application to showcase OpenCQRS features
- Option to automatically validate commands
- Event Grid messaging provider
- Kafka messaging provider
- File store provider for event sourcing
- Amazon SQS messaging provider

📣 _[Release Notes](https://opencqrs.github.io/OpenCQRS/Release-Notes.html)_

## 📦 Nuget Packages

| Package                                                                                                                                               | Latest Stable                                                                                                                                                   | Latest                                                                                                                                                                   |
|-------------------------------------------------------------------------------------------------------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| [OpenCqrs](https://www.nuget.org/packages/OpenCqrs)                                                                                                   | [![Nuget Package](https://img.shields.io/badge/nuget-7.2.0-blue.svg)](https://www.nuget.org/packages/OpenCqrs)                                                  | [![Nuget Package](https://img.shields.io/badge/nuget-7.3.0.preview.1-blue.svg)](https://www.nuget.org/packages/OpenCqrs)                                                  |
| [OpenCqrs.EventSourcing](https://www.nuget.org/packages/OpenCqrs.EventSourcing)                                                                       | [![Nuget Package](https://img.shields.io/badge/nuget-7.2.0-blue.svg)](https://www.nuget.org/packages/OpenCqrs.EventSourcing)                                    | [![Nuget Package](https://img.shields.io/badge/nuget-7.3.0.preview.1-blue.svg)](https://www.nuget.org/packages/OpenCqrs.EventSourcing)                                    |
| [OpenCqrs.EventSourcing.Store.Cosmos](https://www.nuget.org/packages/OpenCqrs.EventSourcing.Store.Cosmos)                                             | [![Nuget Package](https://img.shields.io/badge/nuget-7.2.0-blue.svg)](https://www.nuget.org/packages/OpenCqrs.EventSourcing.Store.Cosmos)                       | [![Nuget Package](https://img.shields.io/badge/nuget-7.3.0.preview.1-blue.svg)](https://www.nuget.org/packages/OpenCqrs.EventSourcing.Store.Cosmos)                       |
| [OpenCqrs.EventSourcing.Store.Cosmos.InMemory](https://www.nuget.org/packages/OpenCqrs.EventSourcing.Store.Cosmos.InMemory)                           |                                                                                                                                                                 | [![Nuget Package](https://img.shields.io/badge/nuget-7.3.0.preview.1-blue.svg)](https://www.nuget.org/packages/OpenCqrs.EventSourcing.Store.Cosmos.InMemory)              |
| [OpenCqrs.EventSourcing.Store.EntityFrameworkCore](https://www.nuget.org/packages/OpenCqrs.EventSourcing.Store.EntityFrameworkCore)                   | [![Nuget Package](https://img.shields.io/badge/nuget-7.2.0-blue.svg)](https://www.nuget.org/packages/OpenCqrs.EventSourcing.Store.EntityFrameworkCore)          | [![Nuget Package](https://img.shields.io/badge/nuget-7.3.0.preview.1-blue.svg)](https://www.nuget.org/packages/OpenCqrs.EventSourcing.Store.EntityFrameworkCore)          |
| [OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Identity](https://www.nuget.org/packages/OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Identity) | [![Nuget Package](https://img.shields.io/badge/nuget-7.2.0-blue.svg)](https://www.nuget.org/packages/OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Identity) | [![Nuget Package](https://img.shields.io/badge/nuget-7.3.0.preview.1-blue.svg)](https://www.nuget.org/packages/OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Identity) |
| [OpenCqrs.Messaging.RabbitMq](https://www.nuget.org/packages/OpenCqrs.Messaging.RabbitMq)                                                             | [![Nuget Package](https://img.shields.io/badge/nuget-7.2.0-blue.svg)](https://www.nuget.org/packages/OpenCqrs.Messaging.RabbitMq)                               | [![Nuget Package](https://img.shields.io/badge/nuget-7.3.0.preview.1-blue.svg)](https://www.nuget.org/packages/OpenCqrs.Messaging.RabbitMq)                               |
| [OpenCqrs.Messaging.ServiceBus](https://www.nuget.org/packages/OpenCqrs.Messaging.ServiceBus)                                                         | [![Nuget Package](https://img.shields.io/badge/nuget-7.2.0-blue.svg)](https://www.nuget.org/packages/OpenCqrs.Messaging.ServiceBus)                             | [![Nuget Package](https://img.shields.io/badge/nuget-7.3.0.preview.1-blue.svg)](https://www.nuget.org/packages/OpenCqrs.Messaging.ServiceBus)                             |
| [OpenCqrs.Validation.FluentValidation](https://www.nuget.org/packages/OpenCqrs.Validation.FluentValidation)                                           | [![Nuget Package](https://img.shields.io/badge/nuget-7.2.0-blue.svg)](https://www.nuget.org/packages/OpenCqrs.Validation.FluentValidation)                      | [![Nuget Package](https://img.shields.io/badge/nuget-7.3.0.preview.1-blue.svg)](https://www.nuget.org/packages/OpenCqrs.Validation.FluentValidation)                      |
| [OpenCqrs.Caching.Redis](https://www.nuget.org/packages/OpenCqrs.Caching.Redis)                                                                       | [![Nuget Package](https://img.shields.io/badge/nuget-7.2.0-blue.svg)](https://www.nuget.org/packages/OpenCqrs.Caching.Redis)                                    | [![Nuget Package](https://img.shields.io/badge/nuget-7.3.0.preview.1-blue.svg)](https://www.nuget.org/packages/OpenCqrs.Caching.Redis)                                    |
| [OpenCqrs.Caching.Memory](https://www.nuget.org/packages/OpenCqrs.Caching.Memory)                                                                     | [![Nuget Package](https://img.shields.io/badge/nuget-7.2.0-blue.svg)](https://www.nuget.org/packages/OpenCqrs.Caching.Memory)                                   | [![Nuget Package](https://img.shields.io/badge/nuget-7.3.0.preview.1-blue.svg)](https://www.nuget.org/packages/OpenCqrs.Caching.Memory)                                   |

## 🔄 Simple mediator

Three kinds of requests can be sent through the dispatcher:

### Commands

```C#
public class DoSomething : ICommand
{
}

public class DoSomethingHandler : ICommandHandler<DoSomething>
{
    private readonly IMyService _myService;

    public DoSomethingHandler(IMyService myService)
    {
        _myService = myService;
    }

    public async Task<Result> Handle(DoSomething command)
    {
        await _myService.MyMethod();

        return Result.Ok();
    }
}

await _dispatcher.Send(new DoSomething());
```

### Queries

```C#
public class Something
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public class GetSomething : IQuery<Something>
{
    public int Id { get; set; }
}

public class GetSomethingQueryHandler : IQueryHandler<GetSomething, Something>
{
    private readonly MyDbContext _dbContext;

    public GetProductsHandler(MyDbContext dbContext)
    {
        _dbContext = dbContext;
    }
        
    public Task<Result<Something>> Handle(GetSomething query)
    {
        return _dbContext.Somethings.FirstOrDefaultAsync(s => s.Id == query.Id);
    }
}

var something = await _dispatcher.Get(new GetSomething { Id = 123 });
```

### Notifications

```C#
public class SomethingHappened : INotifcation
{
}

public class SomethingHappenedHandlerOne : INotifcationHandler<SomethingHappened>
{
    private readonly IServiceOne _serviceOne;

    public SomethingHappenedHandlerOne(IServiceOne serviceOne)
    {
        _serviceOne = serviceOne;
    }

    public Task<Result> Handle(SomethingHappened notification)
    {
        return _serviceOne.DoSomethingElse();
    }
}

public class SomethingHappenedHandlerTwo : INotifcationHandler<SomethingHappened>
{
    private readonly IServiceTwo _serviceTwo;

    public SomethingHappenedHandlerTwo(IServiceTwo serviceTwo)
    {
        _serviceTwo = serviceTwo;
    }

    public Task<Result> Handle(SomethingHappened notification)
    {
        return _serviceTwo.DoSomethingElse();
    }
}

await _dispatcher.Publish(new SomethingHappened());
```

## 💾 Event Sourcing

You can use the `IDomainService` interface to access the event-sourcing functionalities for every store provider.

In the Cosmos DB store provider you can also use the `ICosmosDataStore` interface to access Cosmos DB specific features.

In the Entity Framework Core store provider you can also use the `IDomainDbContext` extensions to access Entity Framework Core specific features.
In the Entity Framework Core store provider, IdentityDbContext from ASP.NET Core Identity is also supported.

### Save Events and Aggregate Snapshot

Defines an aggregate with an event filter and applies events to update its state.

```C#
[AggregateType("Order")]
puclic class Order : AggregateRoot
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
        };);
    }

    protected override bool Apply<T>(T @event)
    {
        return @event switch
        {
            OrderPlaced @event => Apply(@event)
            _ => false
        };
    }

    private bool Apply(OrderPlaced @event)
    {
        OrderId = @event.OrderId;
        Amount = @event.Amount;

        return true;
    }
}

var streamId = new CustomerStreamId(customerId);
var aggregateId = new OrderId(orderId);
var aggregate = new Order(orderId, amount: 25.45m);

// SaveAggregate stores the uncommitted events and the snapshot of the aggregate
var saveAggregateResult = await domainService.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0);

// The alternative is to store the events and the snapshot separately
var saveEventsResult = await domainService.SaveEvents(streamId, aggregate.UncommittedEvents(), expectedEventSequence: 0);
var updateAggregateResult = await domainService.UpdateAggregate(streamId, aggregateId);
```

### Get Aggregate Snapshot

Retrieves an aggregate using one of four read modes, allowing for flexible read/write patterns based on specific needs.

```C#
var streamId = new CustomerStreamId(customerId);
var aggregateId = new OrderAggregateId(orderId);

// Retrieves the aggregate from its snapshot only. If no snapshot exists, returns null.
var aggregate = await domainService.GetAggregate(streamId, aggregateId, ReadMode.SnapshotOnly);

// Retrieves the aggregate from its snapshot if it exists and applies any new events that 
// have occurred since the snapshot. If no snapshot exists, returns null.
var aggregate = await domainService.GetAggregate(streamId, aggregateId, ReadMode.SnapshotWithNewEvents);

// Retrieves the aggregate from its snapshot if it exists; otherwise, reconstructs it from events. 
// If no events exist, returns null.
var aggregate = await domainService.GetAggregate(streamId, aggregateId, ReadMode.SnapshotOrCreate);

// Retrieves the aggregate from its snapshot if it exists, applies any new events that 
// have occurred since the snapshot, or reconstructs it from events if no snapshot exists. 
// If no events exist, returns null.
var aggregate = await domainService.GetAggregate(streamId, aggregateId, ReadMode.SnapshotWithNewEventsOrCreate);
```

### Get InMemory Aggregate

With OpenCQRS, you can replay events in-memory up to a specific event sequence or date, giving you precise control for debugging or auditing.

```C#
var streamId = new CustomerStreamId(customerId);
var aggregateId = new OrderAggregateId(orderId);

// Reconstructs the aggregate from all its events
var aggregate = await domainService.GetInMemoryAggregate(streamId, aggregateId);

// Reconstructs the aggregate up to a specific event sequence number
var aggregate = await domainService.GetInMemoryAggregate(streamId, aggregateId, upToSequence);

// Reconstructs the aggregate up to a specific date
var aggregate = await domainService.GetInMemoryAggregate(streamId, aggregateId, upToDate);
```

### Get Events

Need to inspect your event stream? OpenCQRS makes it easy to retrieve events with flexible querying.

Get all events, filter by sequence, date, or event type. Whether you’re auditing, debugging, or building reports, these methods give you full control over your event history.

```C#
var streamId = new CustomerStreamId(customerId);

// Get all events for the stream
var result = await domainService.GetEvents(streamId);

// Get events from a specific event sequence number
var result = await domainService.GetEventsFromSequence(streamId, fromSequence);

// Get events up to a specific event sequence number
var result = await domainService.GetEventsUpToSequence(streamId, toSequence);

// Get events between two specific event sequence numbers
var result = await domainService.GetEventsBetweenSequences(streamId, fromSequence, toSequence);

// Get events from a specific date
var result = await domainService.GetEventsFromDate(streamId, fromDate);

// Get events up to a specific date
var result = await domainService.GetEventsUpToDate(streamId, toDate);

// Get events between two specific dates
var result = await domainService.GetEventsBetweenDates(streamId, fromDate, toDate);

// Event type filter can be applied to all previous queries
var eventTypes = new Type[] { typeof(OrderPlaced), typeof(OrderShipped) };
var result = await domainService.GetEvents(streamId, eventTypes);
```

📘 _[Full documentation](https://opencqrs.github.io/OpenCQRS/)_

## ✨ Custom Implementations and Project Support

OpenCQRS is designed to be extensible, supporting custom store, messaging, caching, and validation providers. 

Need a specific implementation for your existing code or a new provider (e.g., a custom database store or messaging bus)? I’ve got you covered! 
I can also work directly on your projects to implement OpenCQRS for your specific event sourcing or CQRS needs. 

Please reach out to request custom integrations, new providers, or project assistance via [LinkedIn](https://www.linkedin.com/in/lucabriguglia).
