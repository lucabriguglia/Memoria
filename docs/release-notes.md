---
redirect_from:
  - /Release-Notes.html
  - /Release-Notes/
---

# Release Notes

## Memoria 1.6.0
_**Unreleased**_
- **Cheaper `GetEventsAppliedToAggregate` on the Cosmos DB store.** It fetched the events an aggregate was built from by matching their string ids, which cost more than the ordered read the query already performs. Because every event id this store writes is `{streamId}:{sequence}`, the sequences are recoverable from the ids with no extra read, and matching on the numeric sequence measured 7.68 RU against 10.60 for 150 events — about 16% off the whole operation. An id the store did not write falls back to matching on the id, so documents placed in the container by other means are still found. No API change
- **Oversized Cosmos DB writes are now reported as such, and long streams can be snapshotted.** The Cosmos DB provider writes several documents per event into a transactional batch, which Cosmos caps at 100 operations — so `SaveEvents` failed above 100 events, `SaveAggregate` above 49, and building an aggregate over a stream of 100 events or more failed outright. Every one surfaced as `memoria/storage-failure`, indistinguishable from the database being unreachable. The two appending paths now refuse oversized input up front with a new `memoria/batch-limit-exceeded` failure (`ErrorCode.BadRequest`, tagged with `requestedEventCount` and `maximumEventCount`) naming both counts; they still commit atomically with the sequence check, so their batches cannot be split. The two snapshot-writing paths — the `GetAggregate` cold path and snapshot refresh — now split their writes across as many batches as needed, so a stream of any length can be snapshotted. Those writes go over events that are already durable: link documents are written first and idempotently, the snapshot last, so a failure part-way leaves no snapshot and the next read simply redoes the work. See [Cosmos DB write limits](reference/configuration/cosmos.md#write-limits)
- **The Cosmos DB store now shares one `CosmosClient` across the application.** `CosmosDataStore` and `CosmosDomainService` each constructed and disposed their own client, and both are scoped — so an ASP.NET Core application created and destroyed two clients per request, plus a third on every `CosmosSetup` call. A `CosmosClient` performs its own account discovery, builds its own routing map and, in `Direct` mode (the Memoria default), opens its own connections to every replica it touches; none of that survives disposal. A new `CosmosClientProvider` owns one client and the container resolved from `CosmosOptions`, registered as a singleton by `AddMemoriaCosmos`. `Dispose` on both store types is now a no-op — they still implement `IDisposable`, so existing `using` blocks compile and now stop tearing down connections other scopes are using. Constructing these types directly is a breaking change: see [Upgrade to 1.6.0](guides/upgrade-1.6.0.md#cosmos-client-is-now-shared)
- **A recommended indexing policy for the Cosmos DB container.** `CosmosSetup` creates the container with the Cosmos DB default policy, which indexes every path of every document — including the serialised `data` payload, the largest property in the document, which no Memoria query can filter on (`eventPropertyFilter` compiles to `CONTAINS`, which never uses an index). `scripts/install` now ships a policy that indexes only the seven paths the store filters or sorts on, with idempotent PowerShell and Bash scripts to apply it. Measured against the emulator it takes about 2.4% off writes and 3-6% off reads. It deliberately defines no composite indexes: three were drafted and measured, and they added roughly 7% to every write while returning nothing, because these queries are single-partition and the range index on `sequence` already serves their ordering — the guide carries the numbers. `CosmosSetup.CreateDatabaseAndContainerIfNotExist` now applies it to containers it creates, so a consumer who never reads the guide still gets it; pass `new IndexingPolicy()` to keep the Cosmos DB default. Existing containers are untouched — `CosmosSetup.ReplaceIndexingPolicy` brings one across when you ask, since that starts a background reindex. `CosmosIndexingPolicy.CreateRecommended()` is the policy in code, and a test compares it against the shipped JSON so the two cannot drift. See [Tune the Cosmos DB container](guides/tune-the-cosmos-container.md) and [Upgrade to 1.6.0](guides/upgrade-1.6.0.md#cosmos-containers-get-an-indexing-policy)
- **The Cosmos DB store test suite now runs.** `Memoria.EventSourcing.Store.Cosmos.Tests` referenced `xunit` but not `xunit.runner.visualstudio`, so `dotnet test` discovered no tests and exited zero — the 85 tests it inherits from the shared store suite had never executed. With the runner added they all pass against the Azure Cosmos DB emulator. They need one, and no CI runner provides it, so they carry `[Trait("Category", "Emulator")]` and are excluded from CI: run them locally before changing that store

## Memoria 1.5.0
_**Released 29/08/2026**_
- **Store failures are now classified.** Every event store provider previously returned one indistinguishable failure for every failure path, so a caller could not tell an optimistic concurrency conflict — which is retryable by reloading — from the database being unreachable. Failures now carry an `ErrorCode` and a stable `Type`: `memoria/concurrency-conflict` (`ErrorCode.Conflict`), `memoria/storage-failure` (`ErrorCode.Error`). Constants live on the new `StoreFailures` class. Applied across the Entity Framework Core and Cosmos DB providers together, and asserted in the shared store test suite so providers stay consistent. See [Upgrade to 1.5.0](guides/upgrade-1.5.0.md#store-failures-are-now-classified) for what this may break, and [Failure classification](concepts/result-pattern.md#failure-classification) for the reference
- New `ErrorCode.Conflict`, appended last so the numeric values of existing members are unchanged
- **Saving an aggregate with no uncommitted events now succeeds on every provider**, writing nothing. The Entity Framework Core store previously returned a failure while Cosmos DB returned success, and both already treated `SaveEvents` with an empty array as success — so the Entity Framework Core `SaveAggregate` path was the only one that disagreed, including with its own sibling. If you relied on that failure to detect a command that produced no events, check `UncommittedEvents` yourself before saving
- Failure `Tags` now carry the caller's own context — `streamId`, `expectedEventSequence`, `latestEventSequence` on a conflict, `operation` on a storage failure — plus `traceId` when there is a current `Activity`. A retry can read `latestEventSequence` directly instead of issuing another read. Provider exception detail is deliberately excluded: it names tables, columns and constraints, and a `Failure` mapped onto an HTTP response would disclose it. That detail continues to be recorded on the current `Activity`
- `ErrorHandling.DefaultFailure` on both store providers is superseded by `StoreFailures` and is no longer returned, but remains so existing references compile
- **Event store index changes (Entity Framework Core).** `IX_Events_StreamId_Sequence` is now unique, a new `IX_Events_StreamId_CreatedDate` serves the date-range reads (`GetEventsFromDate`, `GetEventsUpToDate`, `GetEventsBetweenDates`) that previously had to scan a whole stream, and two redundant indexes are dropped: `IX_Events_StreamId` (a prefix of `IX_Events_StreamId_Sequence`) and `IX_AggregateEvents_AggregateId` (the leading column of the composite primary key). No table is rewritten and no data is migrated, but existing databases need the schema change applied — see [Upgrade to 1.5.0](guides/upgrade-1.5.0.md) for the EF migration path and idempotent SQL Server and PostgreSQL scripts
- Faster writes and reads in the Entity Framework Core store: event, aggregate and projection type-binding keys are resolved once per CLR type instead of by reflection on every write; the event-type filter uses a cached reverse index instead of scanning the binding dictionary on every query; and redundant sorts were removed from the aggregate and projection read paths
- The Entity Framework Core store no longer leaves written event and aggregate-event rows attached to the change tracker after a save, so a context reused across many saves no longer accumulates tracked entities
- **The payload serializer is now replaceable.** `IDomainSerializer` controls how event, aggregate and projection payloads are written and read by every store provider, with `DomainSerializer.Current` defaulting to the same Newtonsoft implementation used previously — nothing changes unless you replace it. Replacing it on an existing store is not a free choice: everything already persisted was written by the previous implementation and serializers differ in ways that fail silently, so verify against real stored payloads first
- **Install scripts for the store schema.** Idempotent SQL Server and PostgreSQL scripts under `scripts/install` create the four tables the Entity Framework Core store needs, so a database managed with DbUp, Flyway or by hand can be stood up without writing a migration. Consumers using EF Core migrations need nothing new — `dotnet ef migrations add` already generates the schema from the model, and Memoria deliberately ships no migration files that could drift from it. Both scripts are verified on every CI run by building one database from the script and another from the model and comparing every column and index. See [Install the store schema](guides/install-the-store-schema.md)

## Memoria 1.4.1
_**Released 25/08/2026**_
- `GetProjection` now accepts a `ReadMode` parameter matching the aggregate read modes (`SnapshotOnly`, `SnapshotWithNewEvents`, `SnapshotOrCreate`, `SnapshotWithNewEventsOrCreate`), enabling on-demand projection reconstruction from the event stream and snapshot refresh when new events have arrived; supported by the Entity Framework Core, Npgsql, and Cosmos DB store providers (and their in-memory variants)
- New `GetInMemoryProjection` methods on `IDomainService` that fold matching events into a fresh projection without persisting a snapshot, with overloads for the full stream, up to a specific sequence, and up to a specific date — the projection equivalent of `GetInMemoryAggregate`

## Memoria 1.4.0
_**Released 22/08/2026**_
- New `Projection` read-model base class for building query-optimised read models from events, and a shared `EventSourcedModel` base class (with matching `IEventSourcedModel`/`IProjection` interfaces) that `AggregateRoot` and `Projection` both inherit for stream identity, versioning, and event application. Instance identity stays specific to each: `AggregateId` on `IAggregateRoot`, `ProjectionId` on `IProjection`
- New `SaveProjection` and `GetProjection` methods on `IDomainService` that persist and retrieve projection snapshots, supported by the Entity Framework Core, Npgsql, and Cosmos DB store providers (and their in-memory variants). Each store uses a dedicated projection type: EF Core persists a `ProjectionEntity` in its own `DomainProjections` table, while Cosmos persists a `ProjectionDocument` in the same container as aggregates (discriminated by `documentType`)
- New `[ProjectionType]` attribute and `IProjectionId<T>` identifier for projections; projection types are auto-registered during `AddMemoriaEventSourcing` assembly scanning

## Memoria 1.3.2
_**Released 16/05/2026**_
- Replace Scrutor with a custom scanning mechanism.

## Memoria 1.3.1
_**Released 13/05/2026**_
- `eventPropertyFilter` now works correctly for non-string property values (numbers, booleans, null), not just strings, across the Entity Framework Core, Npgsql, and Cosmos DB store providers
- New `Memoria.EventSourcing.Filtering.EventPropertyFilterValue` helper that coerces a stringly-typed filter value into the matching JSON-scalar literal so filters target the same form Newtonsoft.Json wrote into the event data

## Memoria 1.3.0
_**Released 13/05/2026**_
- New `Memoria.EventSourcing.Store.EntityFrameworkCore.Npgsql` package: replaces the default substring-based event property filter with one that uses the Postgres `@>` JSON-containment operator, so `eventPropertyFilter` works correctly against `jsonb` columns and benefits from GIN indexes
- New `IEventDataFilter` extension point in `Memoria.EventSourcing.Store.EntityFrameworkCore` (in the `Filtering` namespace) for plugging in provider-specific JSON filter strategies; the default `SubstringEventDataFilter` preserves existing behavior on `text` columns
- `EntityFrameworkCoreDomainService` and every `IDomainDbContext` event-query extension method now accept an optional `IEventDataFilter` (non-breaking; defaults to substring)

## Memoria 1.2.1
_**Released 12/05/2026**_
- Dependencies upgrade

## Memoria 1.2.0
_**Released 10/05/2026**_
- Event property filtering on aggregate ids via `IAggregateId.EventPropertyFilter` (key/value pairs applied when retrieving or reconstructing an aggregate)
- New `eventPropertyFilter` parameter across all `IDomainService` event queries (`GetEvents`, `GetEventsFromSequence`, `GetEventsUpToSequence`, `GetEventsBetweenSequences`, `GetEventsFromDate`, `GetEventsUpToDate`, `GetEventsBetweenDates`, `GetLatestEventSequence`)
- Property and type filters can be combined and are supported by both Cosmos DB and Entity Framework Core store providers (and their in-memory variants)

## Memoria 1.1.0
_**Released 01/02/2026**_
- Upgrade to .NET 10
- New In Memory Service Bus provider
- New In Memory RabbitMQ provider

## Memoria 1.0.0
_**Released 10/10/2025**_
- Rename OpenCQRS to Memoria

## OpenCQRS 7.3.0
_**Released 09/10/2025**_
- New Cosmos InMemory store provider

## OpenCQRS 7.2.0
_**Released 27/09/2025**_
- Read mode when getting an aggregate _(BREAKING CHANGE)_:
  - SnapshotOnly
  - SnapshotWithNewEvents
  - SnapshotOrCreate
  - SnapshotWithNewEventsOrCreate

## OpenCQRS 7.1.5
_**Released 15/09/2025**_
- Get aggregate with apply new events false returns now null if the aggregate doesn't exist
- Update aggregate returns null if aggregate doesn't exist

## OpenCQRS 7.1.4
_**Released 13/09/2025**_
- Upgrade Nuget packages to latest versions

## OpenCQRS 7.1.3
_**Released 13/09/2025**_
- UpdateAggregate stores a new aggregate if it doesn't exist
- Minor improvements

## OpenCQRS 7.1.2
_**Released 10/09/2025**_
- Rename IDomainEvent to IEvent

## OpenCQRS 7.1.1
_**Released 10/09/2025**_
- Rename Aggregate to AggregateRoot

## OpenCQRS 7.1.0
_**Released 10/09/2025**_
- New methods in the domain service (EntityFrameworkCore and CosmosDB):
  - Get domain events between two sequences
  - Get domain events up to a specific date
  - Get domain events from a specific date
  - Get domain events between two dates
  - Get in memory aggregate up to a specific date
  - Custom command handlers or services
- Updated XML documentation

## OpenCQRS 7.0.0
_**Released 07/09/2025**_
- Upgrade to .NET 9
- New mediator pattern with commands, queries, and notifications
- Cosmos DB store provider
- Entity Framework Core store provider
- Extensions for db context in the Entity Framework Core store provider
- Support for IdentityDbContext from ASP.NET Core Identity
- Command validation
- Command sequences
- Automatic publishing of notifications and messages (ServiceBus or RabbitMQ) on the back of a successfully processed command
- Automatic caching of query results (MemoryCache or RedisCache)
- More flexible and extensible architecture

## OpenCQRS 7.0.0-rc.1
_**Released 06/09/2025**_
- Memory Caching Provider
- Redis Caching Provider

## OpenCQRS 7.0.0-beta.6
_**Released 05/09/2025**_
- Service Bus Provider
- RabbitMQ Provider
- Automatic publishing of messages on the back of a successfully processed command

## OpenCQRS 7.0.0-beta.5
_**Released 01/09/2025**_
- Cosmos DB store provider

## OpenCQRS 7.0.0-beta.4
_**Released 29/08/2025**_
- Send and publish methods that automatically publish notifications on the back of a successfully processed command
- Automatically validate commands before they are sent to the command handler
- Command sequences that allow to chain multiple commands in a specific order

## OpenCQRS 7.0.0-beta.3
_**Released 26/08/2025**_
- Rename track methods in the Entity Framework Core store provider
- Rename database tables in the Entity Framework Core store provider

## OpenCQRS 7.0.0-beta.2
_**Released 26/08/2025**_
- Replace events with notifications

## OpenCQRS 7.0.0-beta.1 
_**Released 25/08/2025**_
- Complete rewrite of the framework
- Upgrade to .NET 9
