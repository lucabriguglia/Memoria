---
title: Upgrade to 1.5.0
description: "Four changes need attention in Memoria 1.5.0: the first three affect code, the fourth affects your database."
parent: Upgrading
nav_order: 7
---

# Upgrade to 1.5.0

Four changes need attention when upgrading. The first three affect code, the fourth affects your
database.

1. [**Store failures are now classified**](#store-failures-are-now-classified) — if you inspect
   `Failure` to decide what to do next, read this first.
2. [**Saving an aggregate with nothing to save now succeeds**](#nothing-to-save) — it used to fail on
   the Entity Framework Core store.
3. [**`DiagnosticsExtensions.AddException` is no longer an extension method**](#add-exception) — a
   one-line change, and only if you called it yourself.
4. [**Event store index changes**](#event-store-index-changes) — apply to existing Entity Framework
   Core databases.

Nothing else needs action. The payload serializer became replaceable via `IDomainSerializer`, but it
still defaults to the same Newtonsoft implementation, so behaviour is unchanged unless you replace it
— see [Install the store schema](install-the-store-schema.md) and the release notes. The remaining
work is internal: fewer allocations on read and write, and a change tracker that no longer
accumulates saved rows.

<a name="store-failures-are-now-classified"></a>
## Store failures are now classified

Previously every failure path in every store provider returned the same value:

```csharp
new Failure(ErrorCode.Error, "Error", "There was an error when processing the request")
```

So a caller could not tell an optimistic concurrency conflict — which is retryable by reloading —
from the database being unreachable. Failures now carry a stable `Type` and an accurate `ErrorCode`:

| Situation | `Type` | `ErrorCode` |
|---|---|---|
| The stream moved on between reading its sequence and appending | `memoria/concurrency-conflict` | `Conflict` *(new)* |
| The store could not complete the operation | `memoria/storage-failure` | `Error` |

### What might break

- **Code matching on `Failure.Title` or `Failure.Description`.** Both have changed. `Title` is now
  `"Concurrency conflict"` or `"Storage failure"`, and `Description` names the
  stream and, for a conflict, the sequences. Match on `Type` instead — the constants are on
  `StoreFailures` — or on `ErrorCode`.
- **Code assuming `ErrorCode.Error`.** A concurrency conflict is now `ErrorCode.Conflict`. Anything
  treating every store failure as an infrastructure fault will now misclassify a conflict, which is
  the one case that is worth retrying.
- **Switch expressions over `ErrorCode` without a discard arm.** `Conflict` was appended, so existing
  numeric values are unchanged, but a non-exhaustive switch expression warns at compile time
  (`CS8509`) and throws `SwitchExpressionException` at run time if a conflict reaches it. Add a `_`
  arm.
- **`ErrorHandling.DefaultFailure`** on both store providers is superseded and no longer returned. It
  remains so existing references compile.

### What you gain

A conflict is retryable, and the failure now carries what a retry needs:

```csharp
var result = await domainService.SaveAggregate(streamId, aggregateId, order, expectedEventSequence);

if (!result.IsSuccess && result.Failure!.Type == StoreFailures.ConcurrencyConflictType)
{
    var latest = int.Parse(result.Failure.Tags!["latestEventSequence"]);
    // Reload at `latest`, reapply the decision, and save again.
}
```

`Tags` carry your own context — `streamId`, `expectedEventSequence`, `latestEventSequence` on a
conflict, `operation` on a storage failure — plus `traceId` when there is a current `Activity`. They
never carry provider exception detail; that stays on the `Activity`, and `traceId` is the handle that
leads to it. See [Failure classification](../concepts/result-pattern.md#failure-classification).

<a name="nothing-to-save"></a>
## Saving an aggregate with nothing to save now succeeds

Saving an aggregate that has no uncommitted events used to return a failure on the Entity Framework
Core store and success on Cosmos DB. It now returns success on both, writing nothing.

Success is the right answer: no decision was taken, so there was nothing to append, and nothing went
wrong. Both providers already treated `SaveEvents` with an empty array exactly that way — the Entity
Framework Core `SaveAggregate` path was the only one that disagreed, including with its own sibling.

**If you relied on that failure** to detect a command that produced no events — a missing `Add` in an
aggregate method, say — that check has to move into your own code, because the store no longer
reports it. Check `UncommittedEvents` before saving if you want to treat it as an error:

```csharp
if (!order.UncommittedEvents.Any())
{
    // Your call: log it, or treat it as a bug in the command handler.
}
```

<a name="add-exception"></a>
## `DiagnosticsExtensions.AddException` is no longer an extension method

Unrelated to the changes above, and only affects you if you called it directly. Written as an
extension it read as though something were being added to the exception, when what it does is record
the exception against the current `Activity`:

```csharp
// Before
ex.AddException(streamId, "Save Aggregate");

// After
DiagnosticsExtensions.AddException(ex, streamId, "Save Aggregate");
```

Both store providers are affected. Nothing else about it changed — same tags, same `Activity`.

<a name="event-store-index-changes"></a>
## Event store index changes

1.5.0 changes three indexes on the Entity Framework Core event store. Nothing about your events,
aggregates or projections changes — no table is rewritten and no data is migrated — but the schema
your database holds no longer matches the one the package declares until you apply the change.

| Change | Index | Why |
|---|---|---|
| Dropped | `IX_Events_StreamId` | A prefix of `IX_Events_StreamId_Sequence`, so it served no query that index could not, while costing maintenance on every append. |
| Dropped | `IX_AggregateEvents_AggregateId` | Duplicated the leading column of the `DomainAggregateEvents` composite primary key. |
| Now unique | `IX_Events_StreamId_Sequence` | `EventEntity.Id` is derived as `{StreamId}:{Sequence}`, so this pair was already unique. Declaring it lets the database enforce the invariant the store's sequence check assumes. |
| Added | `IX_Events_StreamId_CreatedDate` | `GetEventsFromDate`, `GetEventsUpToDate` and `GetEventsBetweenDates` filter on `CreatedDate`, which nothing indexed. They had to scan a whole stream. |

### If you use EF Core migrations

This is the recommended path, and it needs no SQL from us.

Memoria ships no migrations of its own — your `DbContext` derives from `DomainDbContext` and you own
the migration history. Upgrading the package changes the model, so EF generates the change for you:

```bash
dotnet ef migrations add MemoriaIndexes150
dotnet ef database update
```

The generated migration should contain exactly these operations. If it contains anything else, stop
and check whether your `DbContext` overrides part of the store's model:

```csharp
migrationBuilder.DropIndex(name: "IX_Events_StreamId", table: "events");
migrationBuilder.DropIndex(name: "IX_AggregateEvents_AggregateId", table: "DomainAggregateEvents");

migrationBuilder.DropIndex(name: "IX_Events_StreamId_Sequence", table: "events");
migrationBuilder.CreateIndex(
    name: "IX_Events_StreamId_Sequence",
    table: "events",
    columns: ["StreamId", "Sequence"],
    unique: true);

migrationBuilder.CreateIndex(
    name: "IX_Events_StreamId_CreatedDate",
    table: "events",
    columns: ["StreamId", "CreatedDate"]);
```

### If you do not use EF Core migrations

For databases managed with `EnsureCreated`, DbUp, Flyway, or by hand, apply the script for your
engine:

- [`scripts/migrations/1.5.0-indexes-sqlserver.sql`](../../scripts/migrations/1.5.0-indexes-sqlserver.sql)
- [`scripts/migrations/1.5.0-indexes-postgresql.sql`](../../scripts/migrations/1.5.0-indexes-postgresql.sql)

Both are safe to run more than once, and both begin with a check that fails loudly if duplicate
`(StreamId, Sequence)` rows exist — the unique index cannot be created while they do. Both assume the
default table names and the default schema (`dbo` on SQL Server, `public` on PostgreSQL); adjust the
identifiers if your `DbContext` maps them elsewhere.

> **Do not apply the script if you use EF Core migrations.** Raw DDL applied out of band leaves
> `__EFMigrationsHistory` out of step with the schema, and your next `Add-Migration` will try to make
> the change again.

### Applying it to a large events table

None of these statements rewrite a table, but building an index takes a lock for its duration.

- **SQL Server** — apply during a quiet period, or add `WITH (ONLINE = ON)` to the `CREATE INDEX`
  statements if your edition supports it.
- **PostgreSQL** — `CREATE INDEX CONCURRENTLY` builds without blocking writes, but cannot run inside
  a transaction block. The script's closing comment gives the standalone statements to use instead.
  A `CONCURRENTLY` build that fails leaves an invalid index behind; drop it before retrying.

### Verifying

After applying, `events` should hold `IX_Events_EventType`, `IX_Events_StreamId_CreatedDate` and a
**unique** `IX_Events_StreamId_Sequence`, and `DomainAggregateEvents` should no longer hold
`IX_AggregateEvents_AggregateId`.

Both scripts are exercised against real SQL Server and PostgreSQL containers on every CI run,
starting from a database in the pre-1.5.0 shape, and are checked to produce exactly the index set the
model declares and to be safe to run twice.

## Related

- [Upgrading](../upgrading.md) — every upgrade guide, newest first
- [Upgrade to 1.6.0](upgrade-1.6.0.md) — the next one
- [Release notes](../release-notes.md)
