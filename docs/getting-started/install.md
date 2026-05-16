---
redirect_from:
  - /Installation.html
  - /Installation/
---

# Install

Add the core package to your project:

```
# Package Manager
Install-Package Memoria

# .NET CLI
dotnet add package Memoria

# Paket
paket add Memoria
```

Then pick one of:

- [Quickstart: Mediator](quickstart-mediator.md) — dispatch a command, run a query, publish a notification.
- [Quickstart: Event Sourcing](quickstart-event-sourcing.md) — save an aggregate, reconstruct it, replay events.

## Packages

Only `Memoria` is required. Add the others when you need them.

| Name                                                       | When to install                                                                                  |
|------------------------------------------------------------|--------------------------------------------------------------------------------------------------|
| Memoria                                                    | **Required.** Mediator core: commands, queries, notifications, dispatcher.                       |
| Memoria.EventSourcing                                      | When you want aggregates, streams, and an `IDomainService`. Pairs with one of the stores below. |
| Memoria.EventSourcing.Store.EntityFrameworkCore            | Event sourcing on top of EF Core (SQL Server, SQLite, PostgreSQL, MySQL, In-Memory).            |
| Memoria.EventSourcing.Store.EntityFrameworkCore.Identity   | Above, plus ASP.NET Core Identity in the same DbContext.                                         |
| Memoria.EventSourcing.Store.EntityFrameworkCore.Npgsql     | Adds PostgreSQL `jsonb`-aware event-property filtering to the EF Core store.                     |
| Memoria.EventSourcing.Store.Cosmos                         | Event sourcing on top of Azure Cosmos DB (SQL API).                                              |
| Memoria.EventSourcing.Store.Cosmos.InMemory                | In-process Cosmos stand-in for tests and local dev.                                              |
| Memoria.Messaging.ServiceBus                               | Publish messages to Azure Service Bus when a command succeeds.                                   |
| Memoria.Messaging.ServiceBus.InMemory                      | In-process Service Bus stand-in for tests.                                                       |
| Memoria.Messaging.RabbitMq                                 | Publish messages to RabbitMQ when a command succeeds.                                            |
| Memoria.Messaging.RabbitMq.InMemory                        | In-process RabbitMQ stand-in for tests.                                                          |
| Memoria.Caching.Memory                                     | Cache query results in-process.                                                                  |
| Memoria.Caching.Redis                                      | Cache query results in Redis.                                                                    |
| Memoria.Validation.FluentValidation                        | Auto-validate commands with FluentValidation before they hit the handler.                        |
