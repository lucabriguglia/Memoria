# Memoria&trade;

[![Build](https://github.com/lucabriguglia/Memoria/actions/workflows/build.yml/badge.svg)](https://github.com/lucabriguglia/Memoria/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/Memoria?label=nuget)](https://www.nuget.org/packages/Memoria)
[![Downloads](https://img.shields.io/nuget/dt/Memoria?label=downloads)](https://www.nuget.org/packages/Memoria)
[![Licence](https://img.shields.io/badge/licence-Apache--2.0-blue)](https://www.apache.org/licenses/LICENSE-2.0)

**Event sourcing for .NET with two consistency models in one framework.** Store state as classic
event streams, or as dynamic consistency boundaries where the boundary is a tag query chosen per
decision. Start with the mediator alone and add an event store when you need one — nothing forces
you to take both.

From Latin _memoria_ (memory).

📘 [Documentation](https://lucabriguglia.github.io/Memoria/) ·
🚀 [Getting started](https://lucabriguglia.github.io/Memoria/getting-started/) ·
💡 [Concepts](https://lucabriguglia.github.io/Memoria/concepts/) ·
🧭 [Guides](https://lucabriguglia.github.io/Memoria/guides/) ·
📗 [Reference](https://lucabriguglia.github.io/Memoria/reference/)

📚 [Examples](https://lucabriguglia.github.io/Memoria/examples.html) ·
⬆️ [Upgrading](https://lucabriguglia.github.io/Memoria/upgrading.html) ·
📣 [Release notes](https://lucabriguglia.github.io/Memoria/release-notes.html)

## 📄 Licence at a glance

**The framework is free and open source under the [Apache License 2.0](https://www.apache.org/licenses/LICENSE-2.0)** —
every package under the `Memoria` NuGet prefix, every version. Use it in anything, commercial or
not, closed source or open, at any scale. No edition, no threshold, no key, nothing to sign.

[StateLens](#-statelens), the browser tool that reads a store through your own domain assemblies
and a manifest, is a separate product developed in its own repository: commercial, free for a
single service and paid above that, under [its own licence](https://statelens.dev/docs/licence).
Nothing in this repository is part of it, and installing the packages never requires it.

**Already on 1.x?**
[Upgrade to 2.0.0](https://lucabriguglia.github.io/Memoria/guides/upgrade-2.0.0.html) walks through
the two behaviours that move with the major version. Nothing in the store changes, so there is no
data migration.

## 📥 Install

```bash
dotnet add package Memoria
```

Only `Memoria` is required. Add an event-sourcing package and a store when you need one:

```bash
dotnet add package Memoria.EventSourcing
dotnet add package Memoria.EventSourcing.Store.EntityFrameworkCore
```

The [install guide](https://lucabriguglia.github.io/Memoria/getting-started/install.html) explains
when to reach for each of the [packages listed below](#-packages).

## 🔄 Quickstart

Register what you need at startup:

```csharp
services.AddMemoria(typeof(Program));                    // Mediator: commands, queries, notifications
services.AddMemoriaEventSourcing(typeof(Program));       // Aggregates, streams, projections
services.AddMemoriaEntityFrameworkCore<MyDbContext>();   // Store
```

### As a mediator

```csharp
public record CreateProduct(string Name) : ICommand;

public class CreateProductHandler : ICommandHandler<CreateProduct>
{
    public Task<Result> Handle(CreateProduct command, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Ok());
}

await dispatcher.Send(new CreateProduct("Espresso"));
```

See the [Mediator quickstart](https://lucabriguglia.github.io/Memoria/getting-started/quickstart-mediator.html)
for queries, notifications, validation, custom handlers, command sequences and `SendAndPublish`.

### As an event store

```csharp
[EventType("OrderPlaced")]
public record OrderPlacedEvent(Guid OrderId, decimal Amount) : IEvent;

var streamId = new CustomerStreamId(customerId);
var aggregateId = new OrderId(orderId);
var order = new Order(orderId, amount: 25.45m);

await domainService.SaveAggregate(streamId, aggregateId, order, expectedEventSequence: 0);
```

See the [Event Sourcing quickstart](https://lucabriguglia.github.io/Memoria/getting-started/quickstart-event-sourcing.html)
for the full aggregate definition, and
[Streams or DCB?](https://lucabriguglia.github.io/Memoria/guides/choose-streams-or-dcb.html) for
choosing a consistency model.

Past that, the [guides](https://lucabriguglia.github.io/Memoria/guides/) cover one task each, and
the [reference](https://lucabriguglia.github.io/Memoria/reference/) has the
[`IDomainService` API](https://lucabriguglia.github.io/Memoria/reference/domain-service.html) and a
[configuration page per package](https://lucabriguglia.github.io/Memoria/reference/configuration/).

## ⚡ What you get

| Area | Capabilities |
|------|--------------|
| **Mediator** | Commands, queries and notifications through one `IDispatcher`; command sequences; custom handlers in place of the resolved one; automatic notification publication after a command succeeds; [`Result` instead of exceptions](https://lucabriguglia.github.io/Memoria/concepts/result-pattern.html) |
| **Consistency** | [Event streams](https://lucabriguglia.github.io/Memoria/concepts/aggregates-and-streams.html) with optimistic concurrency on an expected event sequence, or [dynamic consistency boundaries](https://lucabriguglia.github.io/Memoria/concepts/dynamic-consistency-boundaries.html) where the boundary is a tag query chosen per decision; [multiple aggregates per stream](https://lucabriguglia.github.io/Memoria/guides/multiple-aggregates-per-stream.html) |
| **Reads** | [Four read modes](https://lucabriguglia.github.io/Memoria/concepts/read-modes.html); aggregate snapshots stored alongside events for fast, strongly consistent reads; [projections](https://lucabriguglia.github.io/Memoria/concepts/projections.html) persisted and retrieved as snapshots; [in-memory reconstruction](https://lucabriguglia.github.io/Memoria/guides/replay-events-in-memory.html) up to a given sequence or date |
| **Event queries** | Filter applied events by event type, or by event property declared as key/value pairs on the aggregate id; query stream events from or up to a sequence, date or date range; retrieve every event applied to an aggregate |
| **Providers** | Stores: EF Core (plus [ASP.NET Identity](https://lucabriguglia.github.io/Memoria/guides/integrate-aspnet-identity.html) and [PostgreSQL `jsonb`](https://lucabriguglia.github.io/Memoria/guides/use-postgres-jsonb.html) companions), Cosmos DB. Messaging: Azure Service Bus, RabbitMQ. Caching: in-memory, Redis. Validation: FluentValidation |
| **Testing** | [In-memory variants](https://lucabriguglia.github.io/Memoria/guides/test-without-external-deps.html) of Cosmos DB, Service Bus and RabbitMQ, so a test suite needs no external dependency |
| **Tooling** | [StateLens](#-statelens) — a separate browser tool that reads any framework's store through your own domain assemblies and a manifest |

## 🔎 StateLens

[StateLens](https://statelens.dev/) is a browser tool for reading an event sourced store. Point it
at a store, upload a zip of **your own** domain assemblies — with a `statelens.json` at its root
naming the services in it — and it shows you the events that were appended, the aggregates and
projections snapshotted from them, and the types both were written through. Open a row and the
aggregate is folded from its events, so you can see the state a snapshot stands at and how far
behind its history it is. It creates nothing and deletes nothing.

It began here, as Memoria Web, a reader of Memoria stores. It lives in its own repository now, which
is private, and **reads any framework's store, not only Memoria's**: a service's manifest tells the
tool which uploaded types are the streams, events, aggregates and projections, how they are named
and folded, and where in the store the events and snapshots are. Memoria's own stores are described
the same way as anyone else's.

It is a commercial product, separate from the framework: free over a single service, and
[paid above that](https://statelens.dev/pricing), under the
[StateLens Licence](https://statelens.dev/docs/licence). Its [documentation](https://statelens.dev/docs/statelens)
is on its site, and a hosted instance at [demo.statelens.dev](https://demo.statelens.dev) is open by
invitation, asked for through the site's [contact form](https://statelens.dev/contact).

## 📦 Packages

Every package is published at the same version, so one number fits the whole set.

| Package | Version | What it adds |
|---------|---------|--------------|
| [Memoria](https://www.nuget.org/packages/Memoria) | [![NuGet](https://img.shields.io/nuget/v/Memoria)](https://www.nuget.org/packages/Memoria) | **Required.** Mediator core: commands, queries, notifications, dispatcher |
| [Memoria.EventSourcing](https://www.nuget.org/packages/Memoria.EventSourcing) | [![NuGet](https://img.shields.io/nuget/v/Memoria.EventSourcing)](https://www.nuget.org/packages/Memoria.EventSourcing) | Aggregates, streams, projections and `IDomainService` |
| [Memoria.EventSourcing.Dcb](https://www.nuget.org/packages/Memoria.EventSourcing.Dcb) | [![NuGet](https://img.shields.io/nuget/v/Memoria.EventSourcing.Dcb)](https://www.nuget.org/packages/Memoria.EventSourcing.Dcb) | Dynamic consistency boundaries |
| [Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore](https://www.nuget.org/packages/Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore) | [![NuGet](https://img.shields.io/nuget/v/Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore)](https://www.nuget.org/packages/Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore) | EF Core store for the DCB model |
| [Memoria.EventSourcing.Store.EntityFrameworkCore](https://www.nuget.org/packages/Memoria.EventSourcing.Store.EntityFrameworkCore) | [![NuGet](https://img.shields.io/nuget/v/Memoria.EventSourcing.Store.EntityFrameworkCore)](https://www.nuget.org/packages/Memoria.EventSourcing.Store.EntityFrameworkCore) | EF Core store: SQL Server, SQLite, PostgreSQL, MySQL, in-memory |
| [Memoria.EventSourcing.Store.EntityFrameworkCore.Identity](https://www.nuget.org/packages/Memoria.EventSourcing.Store.EntityFrameworkCore.Identity) | [![NuGet](https://img.shields.io/nuget/v/Memoria.EventSourcing.Store.EntityFrameworkCore.Identity)](https://www.nuget.org/packages/Memoria.EventSourcing.Store.EntityFrameworkCore.Identity) | The above, plus ASP.NET Core Identity in the same `DbContext` |
| [Memoria.EventSourcing.Store.EntityFrameworkCore.Npgsql](https://www.nuget.org/packages/Memoria.EventSourcing.Store.EntityFrameworkCore.Npgsql) | [![NuGet](https://img.shields.io/nuget/v/Memoria.EventSourcing.Store.EntityFrameworkCore.Npgsql)](https://www.nuget.org/packages/Memoria.EventSourcing.Store.EntityFrameworkCore.Npgsql) | PostgreSQL `jsonb`-aware event-property filtering |
| [Memoria.EventSourcing.Store.Cosmos](https://www.nuget.org/packages/Memoria.EventSourcing.Store.Cosmos) | [![NuGet](https://img.shields.io/nuget/v/Memoria.EventSourcing.Store.Cosmos)](https://www.nuget.org/packages/Memoria.EventSourcing.Store.Cosmos) | Azure Cosmos DB store (SQL API) |
| [Memoria.EventSourcing.Store.Cosmos.InMemory](https://www.nuget.org/packages/Memoria.EventSourcing.Store.Cosmos.InMemory) | [![NuGet](https://img.shields.io/nuget/v/Memoria.EventSourcing.Store.Cosmos.InMemory)](https://www.nuget.org/packages/Memoria.EventSourcing.Store.Cosmos.InMemory) | In-process Cosmos DB stand-in for tests |
| [Memoria.Messaging.ServiceBus](https://www.nuget.org/packages/Memoria.Messaging.ServiceBus) | [![NuGet](https://img.shields.io/nuget/v/Memoria.Messaging.ServiceBus)](https://www.nuget.org/packages/Memoria.Messaging.ServiceBus) | Publish to Azure Service Bus when a command succeeds |
| [Memoria.Messaging.ServiceBus.InMemory](https://www.nuget.org/packages/Memoria.Messaging.ServiceBus.InMemory) | [![NuGet](https://img.shields.io/nuget/v/Memoria.Messaging.ServiceBus.InMemory)](https://www.nuget.org/packages/Memoria.Messaging.ServiceBus.InMemory) | In-process Service Bus stand-in for tests |
| [Memoria.Messaging.RabbitMq](https://www.nuget.org/packages/Memoria.Messaging.RabbitMq) | [![NuGet](https://img.shields.io/nuget/v/Memoria.Messaging.RabbitMq)](https://www.nuget.org/packages/Memoria.Messaging.RabbitMq) | Publish to RabbitMQ when a command succeeds |
| [Memoria.Messaging.RabbitMq.InMemory](https://www.nuget.org/packages/Memoria.Messaging.RabbitMq.InMemory) | [![NuGet](https://img.shields.io/nuget/v/Memoria.Messaging.RabbitMq.InMemory)](https://www.nuget.org/packages/Memoria.Messaging.RabbitMq.InMemory) | In-process RabbitMQ stand-in for tests |
| [Memoria.Caching.Memory](https://www.nuget.org/packages/Memoria.Caching.Memory) | [![NuGet](https://img.shields.io/nuget/v/Memoria.Caching.Memory)](https://www.nuget.org/packages/Memoria.Caching.Memory) | Cache query results in-process |
| [Memoria.Caching.Redis](https://www.nuget.org/packages/Memoria.Caching.Redis) | [![NuGet](https://img.shields.io/nuget/v/Memoria.Caching.Redis)](https://www.nuget.org/packages/Memoria.Caching.Redis) | Cache query results in Redis |
| [Memoria.Validation.FluentValidation](https://www.nuget.org/packages/Memoria.Validation.FluentValidation) | [![NuGet](https://img.shields.io/nuget/v/Memoria.Validation.FluentValidation)](https://www.nuget.org/packages/Memoria.Validation.FluentValidation) | Validate commands before they reach the handler |

## 🗺️ Roadmap

Shipped work is in the [release notes](https://lucabriguglia.github.io/Memoria/release-notes.html).
Next up, in no fixed order:

- Pipelines
- Option to automatically validate commands
- Event Grid messaging provider
- Kafka messaging provider
- Amazon SQS messaging provider
- File store provider for event sourcing

## 🤝 Contributing

Bug reports, questions and feature requests are welcome as
[issues](https://github.com/lucabriguglia/Memoria/issues) and
[discussions](https://github.com/lucabriguglia/Memoria/discussions). For code, please open an issue
first, and read [CONTRIBUTING.md](https://github.com/lucabriguglia/Memoria/blob/main/CONTRIBUTING.md)
— it covers how to build and test and the conventions to follow. There is nothing to sign: the
framework is Apache 2.0, and a contribution comes in under the same terms it goes out under.

By taking part you agree to the [Code of Conduct](https://github.com/lucabriguglia/Memoria/blob/main/CODE_OF_CONDUCT.md).

## ⭐ Give a star

If Memoria is useful in your learning, samples, workshop or project, please give the repository a
star. Thank you!

## ✨ Custom implementations and project support

Memoria is designed to be extended, with providers for store, messaging, caching and validation.

Need a provider that does not exist yet — a custom database store, a different message bus — or help
applying Memoria to an existing codebase? Get in touch via
[LinkedIn](https://www.linkedin.com/in/lucabriguglia).

## 📄 Licence

**The framework is under the [Apache License 2.0](https://www.apache.org/licenses/LICENSE-2.0)** —
every package published under the `Memoria` NuGet prefix, every version, 1.x and 2.x alike. Build
whatever you like with it, licence that however you like, ship it to whomever you like. The full
text is in [LICENSE.md](https://github.com/lucabriguglia/Memoria/blob/main/LICENSE.md).

**StateLens is a commercial product**, developed in its own private repository and licensed
separately under the [StateLens Licence](https://statelens.dev/docs/licence). Running it is what
the licence governs, metered by *services* — a named set of domain assemblies read over one
connection string. One service is free, and always will be; above that it is priced by how many
services one instance reads, on the tool's [pricing page](https://statelens.dev/pricing), with no
limit on people, instances or environments. Nothing of it is in this repository.

Version 2.0.0-beta was briefly offered under the Reciprocal Public License 1.5 or a commercial
licence covering the packages. That was withdrawn four days later at 2.0.0-beta.2, the framework
returned to Apache 2.0, where it had been throughout 1.x, and 2.0.0 shipped under it. Anyone who
took 2.0.0-beta under the earlier terms keeps them; Apache 2.0 grants strictly more.
