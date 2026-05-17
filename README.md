# 🚀 Memoria&trade;

From Latin _memoria_ (memory)

[![.Build](https://github.com/lucabriguglia/Memoria/actions/workflows/build.yml/badge.svg)](https://github.com/lucabriguglia/Memoria/actions/workflows/build.yml)

.NET framework implementing DDD, CQRS, and Event Sourcing.

Memoria is extremely flexible and expandable. It can be used as a simple mediator or as a full Event Sourcing solution with Cosmos DB or Entity Framework Core as storage.

- 📘 _[Full documentation](https://lucabriguglia.github.io/Memoria/)_
- 📣 _[Release Notes](https://lucabriguglia.github.io/Memoria/release-notes.html)_
- 📚 _[Examples in repository](https://github.com/lucabriguglia/Memoria/tree/main/examples)_
- 🛒 _[EventShop (ecommerce demo application)](https://github.com/lucabriguglia/EventShop)_

## ⭐ Give a star

If you're using this repository for your learning, samples, workshop, or your project, please give a star. Thank you!

## ⚡Main Features

- Mediator with commands, queries, and notifications
- Multiple aggregates per stream
- Option to store the aggregate snapshot alongside events for fast reads and write model strongly consistent
- Four different read modes that allow multiple write/read patterns based on specific needs.
- In memory aggregate reconstruction up to a specific event sequence or date if provided _**(soon up to aggregate version)**_
- Events applied to the aggregate filtered by event type
- Events applied to the aggregate filtered by event property (key/value pairs declared on the aggregate id)
- Retrieval of all events applied to an aggregate
- Querying stream events from or up to a specific event sequence or date/date range
- Querying stream events filtered by event type and/or event property
- Optimistic concurrency control with an expected event sequence
- Automatic event/notification publication after a command is successfully processed that returns a list of results from all notification handlers
- Automatic event/message publication after a command is successfully processed using Service Bus or RabbitMQ
- Automatic command validation with FluentValidation if required
- Command sequences that return a list of results from all commands in the sequence
- Custom command handlers or services can be used instead of the automatically resolved command handlers
- Result pattern across handlers and providers
- Extensible architecture with providers for store, messaging, caching, and validation

## 🗺️ Roadmap

### ✅ Recently Completed
- New Dynamic Consistency Boundary (DCB) packages: a query/tag-based event store that enforces consistency per decision rather than per aggregate, with an Entity Framework Core adapter and a PostgreSQL sibling that uses `pg_advisory_xact_lock` for deadlock-free concurrent writers
- New PostgreSQL companion package for the Entity Framework Core store provider that makes `eventPropertyFilter` work correctly against `jsonb` columns (uses the `@>` JSON-containment operator and is GIN-indexable)
- New `IEventDataFilter` extension point in the Entity Framework Core store provider for plugging in provider-specific JSON filter strategies
- New package for in-memory Service Bus for easier testing in projects using Memoria
- New package for in-memory RabbitMQ for easier testing in projects using Memoria
- Event property filtering across aggregates and stream queries

### ⏭️ Next
- Create an ecommerce demo application to showcase Memoria features

### 🕙 To Follow
- Option to automatically validate commands
- Event Grid messaging provider
- Kafka messaging provider
- File store provider for event sourcing
- Amazon SQS messaging provider

📣 _[Release Notes](https://lucabriguglia.github.io/Memoria/release-notes.html)_

## 📦 Nuget Packages

| Package                                                                                                                                                 | Latest Stable                                                                                                                                                      |
|---------------------------------------------------------------------------------------------------------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| [Memoria](https://www.nuget.org/packages/Memoria)                                                                                                   | [![Nuget Package](https://img.shields.io/badge/nuget-1.3.2-blue.svg)](https://www.nuget.org/packages/Memoria)                                                  |
| [Memoria.EventSourcing](https://www.nuget.org/packages/Memoria.EventSourcing)                                                                       | [![Nuget Package](https://img.shields.io/badge/nuget-1.3.2-blue.svg)](https://www.nuget.org/packages/Memoria.EventSourcing)                                    |
| [Memoria.EventSourcing.Store.Cosmos](https://www.nuget.org/packages/Memoria.EventSourcing.Store.Cosmos)                                             | [![Nuget Package](https://img.shields.io/badge/nuget-1.3.2-blue.svg)](https://www.nuget.org/packages/Memoria.EventSourcing.Store.Cosmos)                       |
| [Memoria.EventSourcing.Store.Cosmos.InMemory](https://www.nuget.org/packages/Memoria.EventSourcing.Store.Cosmos.InMemory)                           | [![Nuget Package](https://img.shields.io/badge/nuget-1.3.2-blue.svg)](https://www.nuget.org/packages/Memoria.EventSourcing.Store.Cosmos.InMemory)              |
| [Memoria.EventSourcing.Store.EntityFrameworkCore](https://www.nuget.org/packages/Memoria.EventSourcing.Store.EntityFrameworkCore)                   | [![Nuget Package](https://img.shields.io/badge/nuget-1.3.2-blue.svg)](https://www.nuget.org/packages/Memoria.EventSourcing.Store.EntityFrameworkCore)          |
| [Memoria.EventSourcing.Store.EntityFrameworkCore.Identity](https://www.nuget.org/packages/Memoria.EventSourcing.Store.EntityFrameworkCore.Identity) | [![Nuget Package](https://img.shields.io/badge/nuget-1.3.2-blue.svg)](https://www.nuget.org/packages/Memoria.EventSourcing.Store.EntityFrameworkCore.Identity) |
| [Memoria.EventSourcing.Store.EntityFrameworkCore.Npgsql](https://www.nuget.org/packages/Memoria.EventSourcing.Store.EntityFrameworkCore.Npgsql)     | [![Nuget Package](https://img.shields.io/badge/nuget-1.3.2-blue.svg)](https://www.nuget.org/packages/Memoria.EventSourcing.Store.EntityFrameworkCore.Npgsql)   |
| [Memoria.EventSourcing.Dcb](https://www.nuget.org/packages/Memoria.EventSourcing.Dcb)                                                               | [![Nuget Package](https://img.shields.io/badge/nuget-1.3.2-blue.svg)](https://www.nuget.org/packages/Memoria.EventSourcing.Dcb)                                |
| [Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore](https://www.nuget.org/packages/Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore)           | [![Nuget Package](https://img.shields.io/badge/nuget-1.3.2-blue.svg)](https://www.nuget.org/packages/Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore)      |
| [Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Npgsql](https://www.nuget.org/packages/Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Npgsql) | [![Nuget Package](https://img.shields.io/badge/nuget-1.3.2-blue.svg)](https://www.nuget.org/packages/Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Npgsql) |
| [Memoria.Messaging.RabbitMq](https://www.nuget.org/packages/Memoria.Messaging.RabbitMq)                                                             | [![Nuget Package](https://img.shields.io/badge/nuget-1.3.2-blue.svg)](https://www.nuget.org/packages/Memoria.Messaging.RabbitMq)                               |
| [Memoria.Messaging.RabbitMq.InMemory](https://www.nuget.org/packages/Memoria.Messaging.RabbitMq.InMemory)                                           | [![Nuget Package](https://img.shields.io/badge/nuget-1.3.2-blue.svg)](https://www.nuget.org/packages/Memoria.Messaging.RabbitMq.InMemory)                      |
| [Memoria.Messaging.ServiceBus](https://www.nuget.org/packages/Memoria.Messaging.ServiceBus)                                                         | [![Nuget Package](https://img.shields.io/badge/nuget-1.3.2-blue.svg)](https://www.nuget.org/packages/Memoria.Messaging.ServiceBus)                             |
| [Memoria.Messaging.ServiceBus.InMemory](https://www.nuget.org/packages/Memoria.Messaging.ServiceBus.InMemory)                                       | [![Nuget Package](https://img.shields.io/badge/nuget-1.3.2-blue.svg)](https://www.nuget.org/packages/Memoria.Messaging.ServiceBus.InMemory)                    |
| [Memoria.Validation.FluentValidation](https://www.nuget.org/packages/Memoria.Validation.FluentValidation)                                           | [![Nuget Package](https://img.shields.io/badge/nuget-1.3.2-blue.svg)](https://www.nuget.org/packages/Memoria.Validation.FluentValidation)                      |
| [Memoria.Caching.Redis](https://www.nuget.org/packages/Memoria.Caching.Redis)                                                                       | [![Nuget Package](https://img.shields.io/badge/nuget-1.3.2-blue.svg)](https://www.nuget.org/packages/Memoria.Caching.Redis)                                    |
| [Memoria.Caching.Memory](https://www.nuget.org/packages/Memoria.Caching.Memory)                                                                     | [![Nuget Package](https://img.shields.io/badge/nuget-1.3.2-blue.svg)](https://www.nuget.org/packages/Memoria.Caching.Memory)                                   |

## 🔄 A taste of the API

### Mediator

```C#
public record CreateProduct(string Name) : ICommand;

public class CreateProductHandler : ICommandHandler<CreateProduct>
{
    public Task<Result> Handle(CreateProduct command) =>
        Task.FromResult(Result.Ok());
}

await dispatcher.Send(new CreateProduct("Espresso"));
```

See the [Mediator Quickstart](https://lucabriguglia.github.io/Memoria/getting-started/quickstart-mediator.html) for queries, notifications, validation, custom handlers, command sequences, and `SendAndPublish`.

### Event Sourcing

```C#
[EventType("OrderPlaced")]
public record OrderPlaced(Guid OrderId, decimal Amount) : IEvent;

var streamId = new CustomerStreamId(customerId);
var aggregateId = new OrderAggregateId(orderId);
var order = new Order(orderId, amount: 25.45m);

await domainService.SaveAggregate(streamId, aggregateId, order, expectedEventSequence: 0);
```

See the [Event Sourcing Quickstart](https://lucabriguglia.github.io/Memoria/getting-started/quickstart-event-sourcing.html) for the full aggregate definition, the four [read modes](https://lucabriguglia.github.io/Memoria/concepts/read-modes.html), [multiple aggregates per stream](https://lucabriguglia.github.io/Memoria/guides/multiple-aggregates-per-stream.html), and in-memory replay.

### Dynamic Consistency Boundary (DCB)

An alternative to aggregate-based event sourcing: consistency is enforced **per decision** rather than per stream. The caller defines a query over events tagged with arbitrary key/value pairs, folds the slice into decision state, and appends with a token that captures the read position. The store rejects the append if any matching event arrived in between.

```C#
[EventType("SeatReserved")]
public record SeatReserved(string Seat) : IEvent;

var query = DcbQuery.Of(new QueryItem(
    [typeof(SeatReserved)],
    TagSet.Of(new("seat", "A12"))));

var result = await store.Decide(
    query: query,
    initialState: false,
    fold: (taken, _) => true,
    decide: taken => taken
        ? Result<IReadOnlyList<EventToAppend>>.Fail(new Failure(Title: "Seat taken"))
        : Result<IReadOnlyList<EventToAppend>>.Ok(
            [EventToAppend.Of(new SeatReserved("A12"), new Tag("seat", "A12"))]));
```

`Memoria.EventSourcing.Dcb` ships an in-memory store; the Entity Framework Core adapter persists events and tags as relational rows; the PostgreSQL sibling adds `pg_advisory_xact_lock`-based concurrency so concurrent writers competing for the same tag set serialize without deadlocks.

📘 _[Full documentation](https://lucabriguglia.github.io/Memoria/)_

## ✨ Custom Implementations and Project Support

Memoria is designed to be extensible, supporting custom store, messaging, caching, and validation providers. 

Need a specific implementation for your existing code or a new provider (e.g., a custom database store or messaging bus)? I’ve got you covered! 
I can also work directly on your projects to implement Memoria for your specific event sourcing or CQRS needs. 

Please reach out to request custom integrations, new providers, or project assistance via [LinkedIn](https://www.linkedin.com/in/lucabriguglia).
