# 🚀 Memoria&trade;

From Latin _memoria_ (memory)

[![.Build](https://github.com/lucabriguglia/Memoria/actions/workflows/build.yml/badge.svg)](https://github.com/lucabriguglia/Memoria/actions/workflows/build.yml)

.NET framework implementing DDD, CQRS, and Event Sourcing.

Memoria is extremely flexible and expandable. It can be used as a simple mediator or as a full Event Sourcing solution with Cosmos DB or Entity Framework Core as storage.

- 📘 _[Full documentation](https://lucabriguglia.github.io/Memoria/)_
- 📣 _[Release Notes](https://lucabriguglia.github.io/Memoria/release-notes.html)_
- 📚 _[Examples in repository](https://github.com/lucabriguglia/Memoria/tree/main/examples)_
- 🛒 _[Ecommerce demo app using DCB](https://github.com/lucabriguglia/Memoria/tree/main/examples/Memoria.Examples.Ecommerce.Dcb)_
- 🔎 _[Memoria Web — browse your store](https://lucabriguglia.github.io/Memoria/tools/memoria-web.html)_

## ⭐ Give a star

If you're using this repository for your learning, samples, workshop, or your project, please give a star. Thank you!

## ⚡Main Features

- Mediator with commands, queries, and notifications
- Two consistency models: event streams, or dynamic consistency boundaries where the boundary is a tag query chosen per decision
- Multiple aggregates per stream
- Option to store the aggregate snapshot alongside events for fast reads and write model strongly consistent
- Projections (read models) persisted and retrieved as snapshots via `SaveProjection` and `GetProjection`
- Four different read modes that allow multiple write/read patterns based on specific needs.
- In memory aggregate and projection reconstruction up to a specific event sequence or date if provided _**(soon up to aggregate version)**_
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
- Memoria Web, a browser tool for reading a store through your own uploaded domain assemblies: the events appended, the aggregates and projections snapshotted from them and the types both were written through, with a snapshot refreshed when it falls behind and any two versions of a model compared side by side

## 🗺️ Roadmap

### ✅ Recently Completed
- Memoria Web, a browser tool that reads a store through domain assemblies uploaded to it
- Ecommerce demo application using DCB
- Dynamic consistency boundaries in their own packages, so a decision whose boundary spans more than one aggregate is expressible without serialising unrelated writes
- New `Projection` read-model base class with `SaveProjection`/`GetProjection` snapshot persistence across all store providers (Entity Framework Core, Npgsql, Cosmos DB, and their in-memory variants)
- New PostgreSQL companion package for the Entity Framework Core store provider that makes `eventPropertyFilter` work correctly against `jsonb` columns (uses the `@>` JSON-containment operator and is GIN-indexable)
- New `IEventDataFilter` extension point in the Entity Framework Core store provider for plugging in provider-specific JSON filter strategies
- New package for in-memory Service Bus for easier testing in projects using Memoria
- New package for in-memory RabbitMQ for easier testing in projects using Memoria
- Event property filtering across aggregates and stream queries

### 🕙 To Follow
- Memoria Web to support multiple services
- Option to automatically validate commands
- Event Grid messaging provider
- Kafka messaging provider
- File store provider for event sourcing
- Amazon SQS messaging provider

📣 _[Release Notes](https://lucabriguglia.github.io/Memoria/release-notes.html)_

## 📦 Nuget Packages

| Package                                                                                                                                             | Latest Stable                                                                                                                                                  |
|-----------------------------------------------------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------|
| [Memoria](https://www.nuget.org/packages/Memoria)                                                                                                   | [![Nuget Package](https://img.shields.io/badge/nuget-1.9.1-blue.svg)](https://www.nuget.org/packages/Memoria)                                                  |
| [Memoria.EventSourcing](https://www.nuget.org/packages/Memoria.EventSourcing)                                                                       | [![Nuget Package](https://img.shields.io/badge/nuget-1.9.1-blue.svg)](https://www.nuget.org/packages/Memoria.EventSourcing)                                    |
| [Memoria.EventSourcing.Dcb](https://www.nuget.org/packages/Memoria.EventSourcing.Dcb)                                                               | [![Nuget Package](https://img.shields.io/badge/nuget-1.9.1-blue.svg)](https://www.nuget.org/packages/Memoria.EventSourcing.Dcb)                                |
| [Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore](https://www.nuget.org/packages/Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore)           | [![Nuget Package](https://img.shields.io/badge/nuget-1.9.1-blue.svg)](https://www.nuget.org/packages/Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore)      |
| [Memoria.EventSourcing.Store.Cosmos](https://www.nuget.org/packages/Memoria.EventSourcing.Store.Cosmos)                                             | [![Nuget Package](https://img.shields.io/badge/nuget-1.9.1-blue.svg)](https://www.nuget.org/packages/Memoria.EventSourcing.Store.Cosmos)                       |
| [Memoria.EventSourcing.Store.Cosmos.InMemory](https://www.nuget.org/packages/Memoria.EventSourcing.Store.Cosmos.InMemory)                           | [![Nuget Package](https://img.shields.io/badge/nuget-1.9.1-blue.svg)](https://www.nuget.org/packages/Memoria.EventSourcing.Store.Cosmos.InMemory)              |
| [Memoria.EventSourcing.Store.EntityFrameworkCore](https://www.nuget.org/packages/Memoria.EventSourcing.Store.EntityFrameworkCore)                   | [![Nuget Package](https://img.shields.io/badge/nuget-1.9.1-blue.svg)](https://www.nuget.org/packages/Memoria.EventSourcing.Store.EntityFrameworkCore)          |
| [Memoria.EventSourcing.Store.EntityFrameworkCore.Identity](https://www.nuget.org/packages/Memoria.EventSourcing.Store.EntityFrameworkCore.Identity) | [![Nuget Package](https://img.shields.io/badge/nuget-1.9.1-blue.svg)](https://www.nuget.org/packages/Memoria.EventSourcing.Store.EntityFrameworkCore.Identity) |
| [Memoria.EventSourcing.Store.EntityFrameworkCore.Npgsql](https://www.nuget.org/packages/Memoria.EventSourcing.Store.EntityFrameworkCore.Npgsql)     | [![Nuget Package](https://img.shields.io/badge/nuget-1.9.1-blue.svg)](https://www.nuget.org/packages/Memoria.EventSourcing.Store.EntityFrameworkCore.Npgsql)   |
| [Memoria.Messaging.RabbitMq](https://www.nuget.org/packages/Memoria.Messaging.RabbitMq)                                                             | [![Nuget Package](https://img.shields.io/badge/nuget-1.9.1-blue.svg)](https://www.nuget.org/packages/Memoria.Messaging.RabbitMq)                               |
| [Memoria.Messaging.RabbitMq.InMemory](https://www.nuget.org/packages/Memoria.Messaging.RabbitMq.InMemory)                                           | [![Nuget Package](https://img.shields.io/badge/nuget-1.9.1-blue.svg)](https://www.nuget.org/packages/Memoria.Messaging.RabbitMq.InMemory)                      |
| [Memoria.Messaging.ServiceBus](https://www.nuget.org/packages/Memoria.Messaging.ServiceBus)                                                         | [![Nuget Package](https://img.shields.io/badge/nuget-1.9.1-blue.svg)](https://www.nuget.org/packages/Memoria.Messaging.ServiceBus)                             |
| [Memoria.Messaging.ServiceBus.InMemory](https://www.nuget.org/packages/Memoria.Messaging.ServiceBus.InMemory)                                       | [![Nuget Package](https://img.shields.io/badge/nuget-1.9.1-blue.svg)](https://www.nuget.org/packages/Memoria.Messaging.ServiceBus.InMemory)                    |
| [Memoria.Validation.FluentValidation](https://www.nuget.org/packages/Memoria.Validation.FluentValidation)                                           | [![Nuget Package](https://img.shields.io/badge/nuget-1.9.1-blue.svg)](https://www.nuget.org/packages/Memoria.Validation.FluentValidation)                      |
| [Memoria.Caching.Redis](https://www.nuget.org/packages/Memoria.Caching.Redis)                                                                       | [![Nuget Package](https://img.shields.io/badge/nuget-1.9.1-blue.svg)](https://www.nuget.org/packages/Memoria.Caching.Redis)                                    |
| [Memoria.Caching.Memory](https://www.nuget.org/packages/Memoria.Caching.Memory)                                                                     | [![Nuget Package](https://img.shields.io/badge/nuget-1.9.1-blue.svg)](https://www.nuget.org/packages/Memoria.Caching.Memory)                                   |

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

📘 _[Full documentation](https://lucabriguglia.github.io/Memoria/)_

## 🔎 Memoria Web

A browser tool for reading a Memoria store. Point it at a database, upload a zip of **your own**
domain assemblies — with a `memoria.json` at its root naming the services in it — and it shows you
the events that were appended, the aggregates and projections snapshotted from them, and the types
both were written through — both consistency models, side by side.

```bash
dotnet run --project src/Memoria.Web
```

![The home page: the streamed model and the DCB model side by side](https://raw.githubusercontent.com/lucabriguglia/Memoria/main/docs/images/memoria-web/home.png)

Every section has a **Types** page, listing what the uploaded assemblies declare, and a **Data**
page, listing what the store actually holds — filtered, sorted and paged, with all of it in the
query string so a view can be bookmarked and shared.

![Aggregate data: rows narrowed by stream, aggregate and identifier](https://raw.githubusercontent.com/lucabriguglia/Memoria/main/docs/images/memoria-web/aggregate-data.png)

Open a row and the aggregate is folded from its events, so you can see the state a snapshot stands
at and how far behind its stream it is.

![An aggregate folded from its events, on the State tab](https://raw.githubusercontent.com/lucabriguglia/Memoria/main/docs/images/memoria-web/aggregate-details.png)

It is in the repository rather than on NuGet, so you build and run it yourself. It creates nothing
and deletes nothing: the only write it offers is refreshing a snapshot that has fallen behind its
stream or its boundary.

> **It has no authentication or authorization yet, and uploading an assembly runs code in its
> process.** Keep it on localhost or behind a proxy that authenticates every request. Both are
> coming in the next release.

To try it without a domain of your own, `src/Memoria.Web.Samples.Streamed` and
`src/Memoria.Web.Samples.Dcb` carry a sample ecommerce domain modelled once in each consistency
model, and `src/Memoria.Web.Samples` fills a store with data written through it.

- 🔎 _[Memoria Web](https://lucabriguglia.github.io/Memoria/tools/memoria-web.html)_ — what it is, and what each page shows
- ⚙️ _[Configuration](https://lucabriguglia.github.io/Memoria/tools/memoria-web-configuration.html)_ · _[Deployment](https://lucabriguglia.github.io/Memoria/tools/memoria-web-deployment.html)_
- 🌱 _[Try it with sample data](https://lucabriguglia.github.io/Memoria/tools/memoria-web-samples.html)_

## ✨ Custom Implementations and Project Support

Memoria is designed to be extensible, supporting custom store, messaging, caching, and validation providers. 

Need a specific implementation for your existing code or a new provider (e.g., a custom database store or messaging bus)? I’ve got you covered! 
I can also work directly on your projects to implement Memoria for your specific event sourcing or CQRS needs. 

Please reach out to request custom integrations, new providers, or project assistance via [LinkedIn](https://www.linkedin.com/in/lucabriguglia).

## 📄 License

Memoria is dual-licensed from version 2.0.0-beta onward. Every 2.x version — alpha, beta and release alike — is offered under **either** of:

- the [Reciprocal Public License 1.5](https://opensource.org/license/rpl-1-5), an OSI-approved open-source licence, if you release the source of the software you build with Memoria under the same licence, or
- the [Memoria Commercial Licence](https://lucabriguglia.github.io/Memoria/license.html), if you do not. Its **Community** edition is free of charge, and always will be, for companies and individuals with less than $5,000,000 USD in annual gross revenue and for registered non-profits with less than $5,000,000 USD in annual total budget. Government or quasi-government agencies do not qualify, and neither does any organisation that has ever received more than $10,000,000 USD in outside capital such as private equity or venture capital. The **Standard**, **Professional** and **Enterprise** editions are subscriptions at $299, $999 and $2,999 USD a year, or $29.90, $99.90 and $299.90 USD a month, half price for anyone who subscribes while 2.0.0 is in beta.

Versions 1.x remain under the [Apache License 2.0](https://github.com/lucabriguglia/Memoria/blob/1.9.1/LICENSE). The full terms are in [LICENSE.md](https://github.com/lucabriguglia/Memoria/blob/main/LICENSE.md) and on the [licence page](https://lucabriguglia.github.io/Memoria/license.html).
