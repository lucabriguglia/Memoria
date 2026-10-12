---
title: Streams or DCB?
description: "How to pick between Memoria's two consistency models — event streams and dynamic consistency boundaries — and when an application can use both."
parent: Guides
nav_order: 8
---

# Streams or DCB?

Memoria ships two consistency models. They are independent packages, they share no tables, and an
application can use both. This is how to pick.

## The short answer

**Stay with streams.** They are simpler, they have three store providers to DCB's one, and most
domains have decisions whose boundary really is one aggregate.

**Reach for DCB when a decision's boundary is not the shape of any stream you can draw** — when it
spans two entities, or differs per decision.

## The test

Write down what one decision must be consistent with, then try to name a stream containing exactly
that and no more.

If you can, use streams. If every candidate stream is either **missing facts the decision needs** or
**dragging in decisions that have nothing to do with it**, that is the DCB case.

The worked example is a student subscribing to a course, where the rule spans a course's capacity and
a student's course count. A stream per course cannot see the student's other subscriptions; a stream
per student cannot see how full the course is; one stream for the school serialises every
subscription in it. See
[Dynamic consistency boundaries](../concepts/dynamic-consistency-boundaries.md).

## Side by side

| | Streams | DCB |
|---|---|---|
| Boundary | A stream, chosen at design time | A tag query, chosen per decision |
| Concurrency | `expectedEventSequence` against one stream | `AppendCondition` against a boundary |
| Ordering | A sequence within each stream | One position across the whole log |
| Contention | Two writes to one stream | Two writes whose boundaries overlap |
| Stores | Entity Framework Core, Cosmos DB, + Identity, + InMemory | Entity Framework Core only |
| Selecting an aggregate's events | `IStreamId`, plus `eventPropertyFilter` for shared streams | Tags |
| Failure on conflict | `memoria/concurrency-conflict` | `memoria/concurrency-conflict` — the same |

## What does not change

Events, `[EventType]` versioning, the fold, `ReadMode`, snapshots, `Result`/`Failure`, the
replaceable serializer, and the store failure classifications are shared. A retry policy keyed on
`memoria/concurrency-conflict` works against either model without modification.

An `IEvent` type can be appended by both. Event bindings are shared for that reason; aggregate and
projection bindings are not, so a streamed `Order` and a DCB `Order` can coexist and neither has to
be renamed.

## Migrating a concept from streams to DCB

**`eventPropertyFilter` becomes a tag.** It exists so several aggregates can share a stream, matching
on a property inside the payload. DCB has no equivalent and does not need one — put the value in a
tag and query it directly:

Both live on the identifier, so the two models line up almost exactly:

```csharp
// Streams: the stream is passed in, and the id narrows to this aggregate's events inside it
public class OrderId(Guid id) : IAggregateId<Order>
{
    public string Id { get; } = id.ToString();
    public IDictionary<string, string>? EventPropertyFilter => new Dictionary<string, string>
    {
        ["OrderId"] = id.ToString()
    };
}

// DCB: the id carries the whole boundary, because tags select on their own
public class OrderId(Guid id) : IDcbAggregateId<Order>
{
    public string Id { get; } = id.ToString();
    public TagQuery Boundary { get; } = TagQuery.AnyOf(new Tag("order", id.ToString()));
}
```

The difference is that a stream still has to be passed alongside the streamed id, while a DCB
identifier is self-contained — `dcb.GetAggregate(new OrderId(id))` needs nothing else. Tags are also
indexed, where the substring match a property filter compiles to on most providers is not.

**A stream id usually becomes one tag.** `customer-42` becomes `customer:42`, and the events that
were in that stream carry that tag. The difference is that they can carry others too.

## What it costs

Measured with [the benchmarks in this repo](https://github.com/lucabriguglia/Memoria/tree/main/benchmarks)
on all three engines the store targets — in-memory SQLite, SQL Server 2022 and PostgreSQL 15, the
last two in containers — with the same event type, the same model state and the same fold on both
sides, so what is measured is the store.

**Reads are a wash on every engine, and long reads favour DCB.** DCB issues the same number of round
trips as streams, and the timings run 0.76× to 1.40× across all three engines and 10, 100 and 1000
events. That spread is the length of the read, not noise: the gap is widest against DCB at ten
events, where the tag lookup's fixed cost has nothing to amortise against, and turns in DCB's favour
by a thousand.

Reading a snapshot is the *same* operation in both stores, so its ratio ought to be exactly 1.00; it
measured 0.99–1.01 on SQL Server, 1.00–1.03 on PostgreSQL and 1.09–1.15 on SQLite. Treat anything
inside those bands as measurement rather than store — including on SQLite, where the read is 22 μs
and a couple of fixed microseconds is 10% of it.

The result that repeats is `GetEvents` over a thousand events, where DCB is the faster of the two on
every engine — 0.97× on SQLite, 0.76× on SQL Server, 0.90× on PostgreSQL — and the fold built on it
follows at 0.95×, 0.77× and 0.93×. DCB also allocates about 3% less on that read, on all three
engines. The crossover is around a hundred events.

A stream is a range on one indexed column; a boundary is a semi-join against `DcbEventTags` over the
same rows. A snapshot read is one row either way, and stays flat however long the history gets —
which is the point of snapshots in both models.

**On SQL Server and PostgreSQL, appends are cheaper under DCB** — three quarters of a streamed append's
time and well under its allocation, flat in the number of events already stored. On SQLite they cost
nearly twice as much:

| `SaveAggregate`, median | Streams        | DCB            | Ratio         | Allocated   |
|-------------------------|----------------|----------------|---------------|-------------|
| SQLite, in memory       | 0.90 – 0.99 ms | 1.53 – 1.91 ms | 1.75–1.90     | 2.06×       |
| SQL Server, container   | 6.80 – 7.05 ms | 5.21 – 5.35 ms | **0.75–0.80** | 0.58–0.59×  |
| PostgreSQL, container   | 3.75 – 3.82 ms | 2.75 – 2.81 ms | **0.74–0.75** | 0.68–0.69×  |

The ranges span 10, 100 and 1000 already-stored events. These are from 2.1.0, in which a DCB append on
SQL Server and PostgreSQL became one call to a routine in the database; the release before was measured
in the same sitting at 1.49–1.55 on SQL Server and 1.94–2.10 on PostgreSQL.

A streamed append is 2 database commands on both real engines; a DCB append is 1 — the routine checks
the boundary, locks its tag heads, then writes the events, their tags and the snapshot, all in one
call (see [How an append is written](../concepts/dynamic-consistency-boundaries.md#how-an-append-is-written)).
SQLite keeps the Entity Framework Core path, 5 commands against 3, because in process a round trip is
almost free and the extra statements cost less than a routine would save.

Read the ratios, not the milliseconds. Two runs of the same commit ten minutes apart once put the SQL
Server streamed append at 13.8 ms and then 18.9 ms; the ratio held across both. The absolute numbers
say more about the machine than about either store. The containers run on the same machine as the
benchmark, so a round trip never leaves it: the further away your database, the more each round trip
costs, and the more one instead of two is worth.

When many writers append to **one** boundary at once, refusals rather than round trips decide the
throughput. Jittered backoff between retries raised the routine's throughput in every case the benchmarks
measured, from 3% against a SQL Server container to twice as much on LocalDB; see [When appends collide](../concepts/dynamic-consistency-boundaries.md#when-appends-collide).

### One PostgreSQL caveat

The DCB read is sensitive to missing planner statistics on PostgreSQL. Both its predicates are
`= ANY(@array)` — the form Npgsql generates for a tag collection at every size, including a single
tag — whose selectivity PostgreSQL cannot estimate from a parameter, so on a table it has never
analysed it can assume one row on each side, pick a nested loop semi join, and apply the position
match as a filter rather than an index condition.

How much that costs varies more than it first appeared. One sitting measured 80 ms against 3 ms for
the same query after `ANALYZE`; a later check on PostgreSQL 15.1, with the statistics suppressed and
verified suppressed, measured 1.20× rather than 21×, because autoanalyze had already given the
planner enough. Treat it as a real effect of unknown size rather than a number.

It resolves itself once autovacuum runs, and SQL Server never shows it because it creates the missing
statistics on first use. It is worth knowing about after a restore, a bulk import or a migration,
where a large table can be queried before it has ever been analysed.

None of this is a reason to pick one or the other. A decision whose boundary is not the shape of any
stream cannot be made correctly under streams at any price, and an append that a stream can express
is no reason on its own to move it to DCB. Pick on the boundary, as above; use these numbers to size the
consequence.

## Known limits

- **A boundary is flat.** `TagQuery.AnyOf` unions its tags and `TagQuery.AllOf` intersects them; a
  query mixing the two — an *or* of *and*s — is not in 1.8.0, and neither is a filter on event type
  inside the boundary, which `EventTypeFilter` on the model does instead.
- **Do not build a catch-up subscription on `Position` yet.** It is monotonic but not gap-free — see
  [Entity Framework Core (DCB)](../reference/configuration/dcb-ef-core.md#positions-are-not-gap-free).
- **One row per distinct tag, forever**, in `DcbTagHeads`.
- **No Cosmos DB provider**, for a structural reason rather than an effort one — see
  [Providers](../concepts/providers.md).

## Using both

Nothing stops you. Register each, and give each its own `DbContext` or apply both sets of entity
configurations to one:

```csharp
services.AddMemoriaEventSourcing(typeof(Program));
services.AddMemoriaEntityFrameworkCore<MyStoreDbContext>();

services.AddMemoriaDcb(typeof(Program));
services.AddMemoriaDcbEntityFrameworkCore<MySchoolDbContext>();
```

Registration is additive and order-independent. The two stores never read each other's tables.

## Related

- [Dynamic consistency boundaries](../concepts/dynamic-consistency-boundaries.md)
- [Aggregates and streams](../concepts/aggregates-and-streams.md)
- [Multiple aggregates per stream](multiple-aggregates-per-stream.md) — the streamed answer to a
  shared boundary, and its limits
