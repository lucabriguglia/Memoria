---
redirect_from:
  - /Store-Providers.html
  - /Store-Providers/
---

# Providers

Memoria is built around a small set of abstractions and **providers** that implement them. The core ships with in-memory or no-op defaults; you replace each provider with a real one only when you need it.

## Provider matrix

| Concern    | Abstraction          | Providers                                                                                                                                                          |
|------------|----------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Storage    | `IDomainService`     | [Entity Framework Core](../reference/configuration/ef-core.md) · [+ Identity](../reference/configuration/ef-core-identity.md) · [Cosmos DB](../reference/configuration/cosmos.md) · Cosmos InMemory |
| Messaging  | `IMessagingProvider` | [Azure Service Bus](../reference/configuration/messaging-servicebus.md) (+ InMemory) · [RabbitMQ](../reference/configuration/messaging-rabbitmq.md) (+ InMemory)   |
| Caching    | `ICachingProvider`   | [In-memory · Redis](../reference/configuration/caching.md)                                                                                                         |
| Validation | `IValidationProvider`| [FluentValidation](../reference/configuration/validation.md)                                                                                                       |

## Why InMemory variants exist

For storage, messaging, and caching, Memoria ships dedicated **InMemory** packages (`Memoria.EventSourcing.Store.Cosmos.InMemory`, `Memoria.Messaging.ServiceBus.InMemory`, `Memoria.Messaging.RabbitMq.InMemory`). These let you run integration tests and local development without standing up the real backing service. The same `IDomainService` / `IMessagingProvider` contract is honored — your application code is unchanged between test and production.

## Pick your storage by use case

- **Relational, transactional writes, complex queries** → Entity Framework Core. SQL Server, SQLite, PostgreSQL, MySQL, and EF Core's In-Memory provider are all supported.
- **Cloud-native, horizontally-scaled writes** → Cosmos DB. The store provider uses transactional batches per stream.
- **ASP.NET Core Identity in the same database** → Entity Framework Core with the [Identity companion package](../reference/configuration/ef-core-identity.md).
- **PostgreSQL with `jsonb` event data** → Entity Framework Core + the [Npgsql companion package](../Entity-Framework-Core-Npgsql.md) so `eventPropertyFilter` translates to the `@>` containment operator.

## Related

- [Configuration: Memoria Core](../reference/configuration/memoria.md)
- [Domain Service](../reference/domain-service.md)
