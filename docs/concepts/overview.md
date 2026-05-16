---
redirect_from:
  - /Basics.html
  - /Basics/
  - /Event-Sourcing.html
  - /Event-Sourcing/
---

# Overview

Memoria is two libraries in one. You can use it as:

1. **A mediator** — dispatch commands, queries, and notifications with a single `IDispatcher` and no further infrastructure.
2. **A full event-sourcing solution** — persist domain events, reconstruct aggregates, and project state with one of several store providers.

These two modes share the same core. You can start with the mediator and add event sourcing later without rewriting handlers.

## Mediator mode

Three kinds of requests flow through the dispatcher:

| Kind             | Interface         | Handlers per request | Returns                      |
|------------------|-------------------|----------------------|------------------------------|
| **Command**      | `ICommand`        | one                  | `Result` or `Result<T>`      |
| **Query**        | `IQuery<T>`       | one                  | `Result<T>`                  |
| **Notification** | `INotification`   | many (fan-out)       | `Result` per handler         |

Every handler returns a [`Result`](result-pattern.md) — failures travel through the type system, not exceptions. See [Configuration: Memoria Core](../reference/configuration/memoria.md) for the one line of DI registration.

## Event sourcing mode

On top of the mediator, Memoria adds aggregates, streams, and an `IDomainService` API:

- **Streams** group related events. See [Aggregates and Streams](aggregates-and-streams.md).
- **Aggregates** derive state from the events in a stream.
- **Snapshots** cache the latest aggregate state so reads don't replay every event — controlled by [read modes](read-modes.md).
- **Multiple aggregates per stream** is supported. Each aggregate filters the stream by event type and (optionally) by event properties.

The actual storage is pluggable. See [Providers](providers.md) for the matrix.

## When to use what

- **Just need to organize commands and queries?** Mediator only. Add validation, messaging, or caching as needed.
- **Need an immutable audit log of business decisions?** Event sourcing.
- **Need to reconstruct historical state ("what did the aggregate look like on day X")?** Event sourcing — see `GetInMemoryAggregate` in the [Domain Service](../reference/domain-service.md).
- **Need CQRS with separate read models?** Either — emit events through messaging and project into your read store.

## Related

- [Aggregates and Streams](aggregates-and-streams.md)
- [Read Modes](read-modes.md)
- [Providers](providers.md)
- [Result Pattern](result-pattern.md)
- [Glossary](glossary.md)
