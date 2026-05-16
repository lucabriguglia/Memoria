# Memoria

Memoria is a .NET framework that can be used as a simple mediator or as an Event Sourcing solution.

**Repository**: [https://github.com/lucabriguglia/Memoria](https://github.com/lucabriguglia/Memoria)

## Documentation

### Concepts

- [Overview](concepts/overview.md) — mediator vs. event sourcing
- [Aggregates and Streams](concepts/aggregates-and-streams.md)
- [Read Modes](concepts/read-modes.md)
- [Providers](concepts/providers.md)
- [Result Pattern](concepts/result-pattern.md)
- [Glossary](concepts/glossary.md)

### Getting started

- [Install](getting-started/install.md)
- [Quickstart: Mediator](getting-started/quickstart-mediator.md)
- [Quickstart: Event Sourcing](getting-started/quickstart-event-sourcing.md)
- [Commands](Commands.md) · [Queries](Queries.md) · [Notifications](Notifications.md) — full feature reference

### Reference

- [Domain Service](reference/domain-service.md)
- [Entity Framework Core Extensions](reference/ef-core-extensions.md)
- Configuration
  - [Memoria Core](reference/configuration/memoria.md)
  - [Event Sourcing](reference/configuration/event-sourcing.md)
    - [Entity Framework Core](reference/configuration/ef-core.md)
    - [+ ASP.NET Core Identity](reference/configuration/ef-core-identity.md)
    - [Cosmos DB](reference/configuration/cosmos.md)
  - [Validation](reference/configuration/validation.md)
  - [Messaging: Service Bus](reference/configuration/messaging-servicebus.md)
  - [Messaging: RabbitMQ](reference/configuration/messaging-rabbitmq.md)
  - [Caching](reference/configuration/caching.md)

### Scenarios

- [Event Sourcing Scenarios](Event-Sourcing-Scenarios.md)
- [Entity Framework Core Scenarios](Entity-Framework-Core-Scenarios.md)
- [PostgreSQL with jsonb](Entity-Framework-Core-Npgsql.md)

---

- [Release Notes](Release-Notes.md)
