| Storage (streams) | `IDomainService`     |---
redirect_from:
  - /Store-Providers.html
  - /Store-Providers/
---

# Providers

Memoria is built around a small set of abstractions and **providers** that implement them. The core ships with in-memory or no-op defaults; you replace each provider with a real one only when you need it.

## Provider matrix

| Concern    | Abstraction          | Providers                                                                                                                                                          |
|------------|----------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Storage (streams) | `IDomainService`  | [Entity Framework Core](../reference/configuration/ef-core.md) · [+ Identity](../reference/configuration/ef-core-identity.md) · [Cosmos DB](../reference/configuration/cosmos.md) · Cosmos InMemory |
| Storage (DCB) | `IDcbDomainService` | [Entity Framework Core](../reference/configuration/dcb-ef-core.md) — relational only, [see below](#why-there-is-no-cosmos-db-provider-for-dcb) |
| Messaging  | `IMessagingProvider` | [Azure Service Bus](../reference/configuration/messaging-servicebus.md) (+ InMemory) · [RabbitMQ](../reference/configuration/messaging-rabbitmq.md) (+ InMemory)   |
| Caching    | `ICachingProvider`   | [In-memory · Redis](../reference/configuration/caching.md)                                                                                                         |
| Validation | `IValidationProvider`| [FluentValidation](../reference/configuration/validation.md)                                                                                                       |

## Why InMemory variants exist

For storage, messaging, and caching, Memoria ships dedicated **InMemory** packages (`Memoria.EventSourcing.Store.Cosmos.InMemory`, `Memoria.Messaging.ServiceBus.InMemory`, `Memoria.Messaging.RabbitMq.InMemory`). These let you run integration tests and local development without standing up the real backing service. The same `IDomainService` / `IMessagingProvider` contract is honored — your application code is unchanged between test and production.

## Pick your storage by use case

- **Relational, transactional writes, complex queries** → Entity Framework Core. SQL Server, SQLite, PostgreSQL, MySQL, and EF Core's In-Memory provider are all supported.
- **Cloud-native, horizontally-scaled writes** → Cosmos DB. The store provider uses transactional batches per stream.
- **ASP.NET Core Identity in the same database** → Entity Framework Core with the [Identity companion package](../reference/configuration/ef-core-identity.md).
- **PostgreSQL with `jsonb` event data** → Entity Framework Core + the [Npgsql companion package](../guides/use-postgres-jsonb.md) so `eventPropertyFilter` translates to the `@>` containment operator.
- **A decision whose boundary spans more than one aggregate** → [dynamic consistency boundaries](dynamic-consistency-boundaries.md), on Entity Framework Core. See [Streams or DCB?](../guides/choose-streams-or-dcb.md).

<a name="why-there-is-no-cosmos-db-provider-for-dcb"></a>
## Why there is no Cosmos DB provider for DCB

This is a structural limit, not a gap in the roadmap.

A DCB append must do three things atomically: evaluate a tag query across the whole log, compare that
boundary's head to what the decision read, and write the events with their tag rows. Cosmos DB offers
atomicity only inside a **transactional batch**, which is scoped to one logical partition — and a tag
query is not partition-scoped by construction. A boundary over `course:c1 OR student:s7` spans
whatever partitions those events happen to live in.

Making it correct would mean forcing every event in the application into a single logical partition,
capping the store at Cosmos DB's per-partition limits (20 GB, and the throughput of one physical
partition) and making every append contend on one partition key — the opposite of what DCB is for.
Shipping that would be shipping a store that looks like it works and falls over at a size nobody
documented.

The streamed model has no such problem, because a stream *is* a natural partition key. That is why
[Cosmos DB](../reference/configuration/cosmos.md) remains a first-class store there. Relational
engines have no equivalent obstacle: one transaction spans the whole table.

## Related

- [Configuration: Memoria Core](../reference/configuration/memoria.md)
- [Domain Service](../reference/domain-service.md)
