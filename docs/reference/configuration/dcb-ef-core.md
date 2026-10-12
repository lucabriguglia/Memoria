---
title: Entity Framework Core (DCB)
description: "Register the Entity Framework Core store for dynamic consistency boundaries, which is independent of the streamed store."
parent: Configuration
grand_parent: Reference
nav_order: 6
---

# Configuration: Entity Framework Core (DCB)

`Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore` is the store for
[dynamic consistency boundaries](../../concepts/dynamic-consistency-boundaries.md). It is independent
of `Memoria.EventSourcing.Store.EntityFrameworkCore`: installing one pulls in nothing of the other.

## Registration

```csharp
services.AddDbContext<SchoolDbContext>(options => options.UseSqlServer(connectionString));

services.AddMemoriaDcb(typeof(Program));
services.AddMemoriaDcbEntityFrameworkCore<SchoolDbContext>();
```

`AddMemoriaDcb` scans for events, DCB aggregates and DCB projections and registers an
`IDcbDomainService` that throws until a store replaces it. `AddMemoriaDcbEntityFrameworkCore`
replaces it. Call it after.

Both are safe to call alongside `AddMemoriaEventSourcing` and `AddMemoriaEntityFrameworkCore`, in
either order.

Your context derives from `DcbDbContext` and needs nothing else:

```csharp
public class SchoolDbContext(
    DbContextOptions<DcbDbContext> options,
    TimeProvider timeProvider,
    IHttpContextAccessor httpContextAccessor)
    : DcbDbContext(options, timeProvider, httpContextAccessor);
```

### Batch limit

An append commits at most `DcbDbContextExtensions.DefaultMaxEventsPerAppend` (1000) events. There is
no hard relational limit the way Cosmos DB caps a transactional batch; this guards an unbounded
append. Override it per registration:

```csharp
services.AddMemoriaDcbEntityFrameworkCore<SchoolDbContext>(maxEventsPerAppend: 200);
```

Exceeding it fails with `memoria/batch-limit-exceeded` before anything is written.

### Retrying transient failures

A managed database drops connections for reasons neither side chose — a failover, a spell of
maintenance, a pooled connection closed at the far end while it sat idle. Configure the provider to
try those again as you would for any other context:

```csharp
services.AddDbContext<SchoolDbContext>(options =>
    options.UseSqlServer(connectionString, sqlServer => sqlServer.EnableRetryOnFailure()));
```

An append runs as one retriable unit, so this needs nothing from you. On SQL Server and PostgreSQL
the unit is the call to the append routine, together with saving any changes staged on the context.
On the Entity Framework Core path it starts where the boundary is read and the tag heads are claimed,
not where the transaction opens: the tokens read there are what the append is guarded on, so an
attempt made again reads them afresh rather than reporting a conflict with an append nobody made.

> **This did not used to work.** EF Core refuses to open a caller's own transaction under a strategy
> that retries, and an append opens one — so every append against a retrying registration failed
> with `memoria/storage-failure`, whatever was wrong. If you have been unable to enable retries,
> this is why.

What a retry cannot decide for you is whether an append that was already committed is safe to make
again. A **conditioned** append answers that itself: if the first attempt committed and only its
acknowledgement was lost, the boundary has moved and the attempt made again is refused with
`memoria/concurrency-conflict` rather than writing twice. An **unconditional** append has nothing to
be refused by, so if you care about exactly-once under retries, give it a condition.

## Schema

Four tables, sharing nothing with the streamed store's three, and on SQL Server and PostgreSQL an
append routine.

| Table | Holds |
|---|---|
| `DcbEvents` | One row per appended event, keyed on a store-assigned `bigint` `Position` |
| `DcbEventTags` | One row per tag on an event, keyed `(Tag, Position)` |
| `DcbTagHeads` | One row per tag ever written under or conditioned on, carrying a concurrency token |
| `DcbSnapshots` | Persisted folds, aggregates and projections alike |

`DcbEventTags` is keyed tag-first because every read narrows by tag before position or date, so the
primary key is also the serving index.

The append routine — `memoria_dcb_append_v1`, a stored procedure on SQL Server and a function on
PostgreSQL — is what every append on those engines calls: one round trip that checks the boundary,
locks its tag heads and writes. It is generated from the context's model, so it follows remapped
tables, columns and schemas and the `jsonb` payload override; a context with any such mapping gets a
routine of its own, named after a hash of the mapping. SQLite has none and appends through Entity
Framework Core, as does a context that implements `IDcbDbContext` without deriving from
`DcbDbContext`. See [How an append is written](../../concepts/dynamic-consistency-boundaries.md#how-an-append-is-written).

Install the tables and the routine with [EF Core migrations, the shipped scripts or
`EnsureDcbAppendRoutineAsync`](../../guides/install-the-store-schema.md#the-dynamic-consistency-boundary-store).

### How a boundary becomes SQL

Every read — `GetEvents*`, `GetLatestPosition`, `GetInMemory*`, and the cheap half of the append
condition — narrows from one query over `DcbEvents`, so both boundary shapes are translated in one
place.

| Boundary | SQL | Cost |
|---|---|---|
| `AnyOf(a, b, …)` | one `EXISTS` over the event's tags, matching an `IN` list | one index seek, whatever the number of tags |
| `AllOf(a, b, …)` | one `EXISTS` per tag, chained | one index seek per tag, intersected by the engine |

Both are semi-joins rather than joins, so an event carrying several of a boundary's tags comes back
once — a join would return it per matching tag row and the fold would apply it twice.

Every seek is against the `(Tag, Position)` primary key, so neither shape needs an index of its own.

A position bound — `GetEventsFromPosition` and friends — goes *inside* the `EXISTS`, alongside the
tag, rather than onto the events it selects. Both are correct, because the subquery equates the two
positions, but only this gives that key both halves of a seek instead of leaving the engine to infer
the second. It is the read after a snapshot, which asks for a suffix of a boundary whose whole
history may be long.

An unbounded read stays a bare `EXISTS` with nothing added. Selecting the events by a materialised
`Position IN (…)` instead was measurably slower on small boundaries — around 20% on a ten-event fold
— and faster on large ones, and a boundary is normally small by construction. See
[the benchmarks](https://github.com/lucabriguglia/Memoria/tree/main/benchmarks).

`GetLatestPosition` never reads `DcbEvents` at all unless it is given an event type filter: a tag row
and the event it tags commit together, so the highest tagged position *is* the boundary's latest
position, and that is a backward seek per tag rather than a semi-join against the log. It runs inside
every conditioned append's transaction, so what it costs is what every overlapping append waits for.
An intersection's cost grows with the number of tags it names, which is a reason to keep a boundary
to the things a decision is actually about rather than a reason to avoid it: a boundary naming twenty
tags is a modelling problem before it is a query-plan one.

### Tag columns are case-sensitive, deliberately

Tags compare ordinally in .NET, so `seat:A1` and `seat:a1` are two tags. The store pins a
case-sensitive collation on both `Tag` columns — `SQL_Latin1_General_CP1_CS_AS` on SQL Server, `"C"`
on PostgreSQL — because SQL Server's usual default is case-*in*sensitive and would make them one row.

That is a correctness property, not tidiness. Under a case-insensitive column a boundary folds in
events it does not own, and an append is refused by a conflict that does not exist. Container tests
assert the collation on both engines and, separately, the behaviour it protects.

Override `DcbDbContext.TagCollation` to pin a different one. Returning `null` accepts the database
default, which you should do only knowing the above.

### Identifier lengths

| Column | Width |
|---|---|
| `DcbEvents.EventType` | 255 |
| `DcbEventTags.Tag`, `DcbTagHeads.Tag` | 255 |
| `DcbSnapshots.StoreId` | 255 |
| `DcbSnapshots.Id` | 400 |

`(Tag, Position)` is 255 × 2 bytes plus 8 — 518 against SQL Server's 900-byte index key limit, with
room to spare. A container test appends a tag at its full width against a live SQL Server.

<a name="positions-are-not-gap-free"></a>
## Positions are not gap-free

`Position` is an identity column, so concurrent transactions take positions in one order and commit
in another. A reader can briefly see position 11 without 10, and 10 appears afterwards.

**That is safe for the append condition**, which is only ever evaluated inside the transaction
holding the relevant tag head rows: if two appends share a tag, one fails outright; if they share
none, neither can see the other's events and the gap is invisible to both.

**It is not safe for a catch-up subscription.** Polling `DcbEvents` ordered by `Position` and
remembering how far you got will silently skip events that commit late. This store deliberately ships
no subscription API, and you should not build one on `Position` alone without a gap-resolution
strategy of your own.

## Diagnostics

| Activity event | When |
|---|---|
| `Aggregate Folded` | A fold is written to a snapshot — same name the streamed stores use |
| `Concurrency Conflict` | An append is refused because its boundary moved |

See [Observability](../observability.md).

## Failures

| Type | Meaning |
|---|---|
| `memoria/concurrency-conflict` | The boundary moved. Tagged `tagQuery`, `expectedPosition`, `latestPosition` |
| `memoria/batch-limit-exceeded` | Too many events in one append |
| `memoria/storage-failure` | Anything else. Provider detail goes to the trace, never the failure. A missing append routine lands here too, with a description naming the routine and how to install it |

The `Type` constants are the ones the streamed stores use, so a retry policy works against both.

## Related

- [Dynamic consistency boundaries](../../concepts/dynamic-consistency-boundaries.md)
- [Streams or DCB?](../../guides/choose-streams-or-dcb.md)
- [Use PostgreSQL with `jsonb`](../../guides/use-postgres-jsonb.md#the-dynamic-consistency-boundary-store)
- [Install the store schema](../../guides/install-the-store-schema.md)
