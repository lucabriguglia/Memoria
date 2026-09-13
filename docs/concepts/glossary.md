# Glossary

Definitions for terms used across the Memoria documentation. See also [Overview](overview.md) and [Aggregates and Streams](aggregates-and-streams.md).

## Administrator

*Memoria Web.* The role that may also install, remove and reread the uploaded assemblies on the Settings page — which is running code on the host. Includes [Updater](#updater). Granted by mapping a claim value under `Authorization:Roles:Administrator` — see [Roles](../tools/memoria-web-configuration.md#roles).

## Aggregate

A consistency boundary that derives its state by applying domain events. Inherits from `AggregateRoot`. Decides which events it cares about via `EventTypeFilter`, and produces new events via `Add(@event)`.

## Aggregate Id

A unique identifier for an aggregate instance within a stream. Implements `IAggregateId<TAggregate>`. May declare an optional `EventPropertyFilter` to disambiguate when multiple aggregates share the same stream and the same event types.

## Domain Event

An immutable fact about something that happened in the domain (e.g. `OrderPlaced`, `OrderShipped`). Implements `IEvent` and is decorated with `[EventType("Name")]` for type binding.

## Event Property Filter

Optional dictionary of key/value pairs declared on an `IAggregateId`. When the aggregate is loaded or reconstructed, only events whose serialized properties match every entry are applied. Use it when multiple aggregate instances live on the same stream — e.g. several orders for the same customer.

## Event Type Filter

A list of `Type` declared on an aggregate. Only events whose CLR type appears in the filter are applied to that aggregate. An empty filter applies all events.

## Expected Event Sequence

The sequence number a writer expects the stream to be at when persisting new events. Memoria's storage providers use this for optimistic concurrency: if another writer has appended in between, the write fails with a concurrency exception.

## In-Memory Aggregate

An aggregate reconstructed entirely from events, with no snapshot involved. Useful for auditing, historical replay, or rebuilding state at a specific point in time (`upToSequence` or `upToDate`).

## Notification

A fan-out message. Multiple `INotificationHandler<T>` handlers can be registered for the same notification; the dispatcher invokes them all and returns the list of results.

## Operator

*Memoria Web.* A person signed in to the tool. Every operator holds one of three roles, each including the one before it: [Reader](#reader), [Updater](#updater), [Administrator](#administrator).

## Projection

A read model: a query-optimised view built by applying domain events. Inherits from `Projection` and, like an aggregate, declares an `EventTypeFilter` and `Apply` — but produces no events (no `Add`, no uncommitted events). Persisted and retrieved as a snapshot via `SaveProjection` / `GetProjection`. See [Projections](projections.md).

## Projection Id

A unique identifier for a projection snapshot, serving as its persistence key. Implements `IProjectionId<TProjection>`. May declare an optional `EventPropertyFilter` to disambiguate when multiple projections share the same stream and the same event types. The projection type is given a stable, versioned logical name with `[ProjectionType("Name")]`.

## Read Mode

Controls how `IDomainService.GetAggregate` reconstructs an aggregate. Four variants trade off freshness against I/O — see [Read Modes](read-modes.md).

## Reader

*Memoria Web.* The role every signed-in [operator](#operator) holds: may read every page and nothing more. An operator whose claims match no mapping is a Reader.

## Result Pattern

`Result` and `Result<T>` are discriminated unions of `Success` / `Failure`. Memoria returns them from every handler and provider operation instead of throwing — see [Result Pattern](result-pattern.md).

## Snapshot

The persisted latest state of an aggregate, stored alongside its event stream. Snapshots make reads fast: instead of replaying every event in the stream, the framework loads the snapshot and (depending on [read mode](read-modes.md)) optionally applies any newer events.

## Stream Id

A unique identifier for an event stream. Implements `IStreamId`. A stream typically groups events related to one entity (e.g. one customer, one tenant), but can hold events for multiple aggregates that filter the stream differently.

## Updater

*Memoria Web.* The role that may also press **Update** on a model's detail page, which writes a refreshed snapshot. Includes [Reader](#reader); included in [Administrator](#administrator). Granted by mapping a claim value under `Authorization:Roles:Updater` — see [Roles](../tools/memoria-web-configuration.md#roles).
