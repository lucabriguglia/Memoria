---
title: Home
description: "Memoria is a .NET framework for DDD, CQRS and Event Sourcing: a mediator on its own, or an event store with classic event streams or dynamic consistency boundaries."
nav_order: 1
---

# Memoria

Memoria is a .NET framework for DDD, CQRS and Event Sourcing. Use it as a plain mediator — commands,
queries and notifications behind one dispatcher — or as an event store, with classic event streams or
with dynamic consistency boundaries where the boundary is a tag query chosen per decision.

```bash
dotnet add package Memoria
```

## Where to start

| You want to… | Start at |
|--------------|----------|
| Dispatch commands and queries, nothing more | [Quickstart: Mediator](getting-started/quickstart-mediator.md) |
| Save and rebuild aggregates from events | [Quickstart: Event Sourcing](getting-started/quickstart-event-sourcing.md) |
| Understand what the pieces are before writing any | [Concepts: Overview](concepts/overview.md) |
| Decide how to model consistency | [Streams or DCB?](guides/choose-streams-or-dcb.md) |
| Look up an API or a settings key | [Reference](reference/) |
| Read a store you already have | [StateLens](tools/index.md), a separate tool |
| Move from an earlier version | [Upgrading](upgrading.md) |

Everything is in the sidebar too: [Guides](guides/) for a task you already know you want to do,
[Concepts](concepts/) for why things are shaped the way they are, and [Reference](reference/) for
looking something up.

## What it does

- **Mediator** — commands, queries and notifications, each with a handler, each returning a
  [`Result`](concepts/result-pattern.md) rather than throwing. Command sequences, custom handlers and
  automatic notification publication come with it.
- **Two consistency models** — [event streams](concepts/aggregates-and-streams.md) with optimistic
  concurrency, or [dynamic consistency boundaries](concepts/dynamic-consistency-boundaries.md).
  Multiple aggregates can share one stream.
- **Snapshots that stay consistent** — an aggregate's snapshot is stored alongside its events, so a
  read is fast without being stale. [Projections](concepts/projections.md) are persisted the same way.
- **[Four read modes](concepts/read-modes.md)** — snapshot only, snapshot with new events, and the
  create-if-missing variants of both.
- **Providers for everything external** — Entity Framework Core and Cosmos DB for storage, Azure
  Service Bus and RabbitMQ for messaging, in-process and Redis for caching, FluentValidation for
  validation. Each has an [in-memory variant](guides/test-without-external-deps.md) so tests need
  nothing running.

## Elsewhere

- [Repository](https://github.com/lucabriguglia/Memoria) ·
  [Examples](examples.md) ·
  [Release notes](release-notes.md)
- [Licence](license.md) — the framework is Apache 2.0; StateLens is a separate commercial product,
  free for one service
- [StateLens](tools/index.md) — the browser tool for reading any framework's store, with a site of
  its own at [statelens.dev](https://statelens.dev/); a hosted
  [demo](https://demo.statelens.dev) is open by invitation
- [Contributing](https://github.com/lucabriguglia/Memoria/blob/main/CONTRIBUTING.md)
