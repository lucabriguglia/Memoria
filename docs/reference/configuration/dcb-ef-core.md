# Configuration: Entity Framework Core (DCB)

`Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore` is the store for
[dynamic consistency boundaries](../../concepts/dynamic-consistency-boundaries.md). It is independent
of `Memoria.EventSourcing.Store.EntityFrameworkCore`: installing one pulls in nothing of the other.

## Registration

```C#
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

```C#
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

```C#
services.AddMemoriaDcbEntityFrameworkCore<SchoolDbContext>(maxEventsPerAppend: 200);
```

Exceeding it fails with `memoria/batch-limit-exceeded` before anything is written.

## Schema

Four tables, sharing nothing with the streamed store's three.

| Table | Holds |
|---|---|
| `DcbEvents` | One row per appended event, keyed on a store-assigned `bigint` `Position` |
| `DcbEventTags` | One row per tag on an event, keyed `(Tag, Position)` |
| `DcbTagHeads` | One row per tag ever written under or conditioned on, carrying a concurrency token |
| `DcbSnapshots` | Persisted folds, aggregates and projections alike |

`DcbEventTags` is keyed tag-first because every read narrows by tag before position or date, so the
primary key is also the serving index.

Install with [EF Core migrations or the shipped scripts](../../guides/install-the-store-schema.md#the-dynamic-consistency-boundary-store).

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
| `memoria/storage-failure` | Anything else. Provider detail goes to the trace, never the failure |

The `Type` constants are the ones the streamed stores use, so a retry policy works against both.

## Related

- [Dynamic consistency boundaries](../../concepts/dynamic-consistency-boundaries.md)
- [Streams or DCB?](../../guides/choose-streams-or-dcb.md)
- [Use PostgreSQL with `jsonb`](../../guides/use-postgres-jsonb.md#the-dynamic-consistency-boundary-store)
- [Install the store schema](../../guides/install-the-store-schema.md)
