# Memoria

Memoria is a .NET framework that can be used as a simple mediator or as an Event Sourcing solution.

**Repository**: [https://github.com/lucabriguglia/Memoria](https://github.com/lucabriguglia/Memoria)

## Documentation

### Getting started

- [Install](getting-started/install.md)
- [Quickstart: Mediator](getting-started/quickstart-mediator.md)
- [Quickstart: Event Sourcing](getting-started/quickstart-event-sourcing.md)

### Concepts

- [Overview](concepts/overview.md) — mediator vs. event sourcing
- [Aggregates and Streams](concepts/aggregates-and-streams.md)
- [Read Modes](concepts/read-modes.md)
- [Providers](concepts/providers.md)
- [Result Pattern](concepts/result-pattern.md)
- [Glossary](concepts/glossary.md)

### Guides

- [Validate commands](guides/validate-commands.md)
- [Use a custom command handler](guides/custom-command-handlers.md)
- [Run a sequence of commands](guides/command-sequences.md)
- [Publish to Service Bus](guides/publish-to-service-bus.md) · [Publish to RabbitMQ](guides/publish-to-rabbitmq.md)
- [Cache query results](guides/cache-queries.md)
- [Multiple aggregates per stream](guides/multiple-aggregates-per-stream.md)
- [Replay events in memory](guides/replay-events-in-memory.md)
- [Use PostgreSQL with jsonb](guides/use-postgres-jsonb.md)
- [Integrate with ASP.NET Core Identity](guides/integrate-aspnet-identity.md)
- [Test without external dependencies](guides/test-without-external-deps.md)

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

---

- [Examples](examples.md)
- [Release Notes](Release-Notes.md)
