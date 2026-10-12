---
title: Upgrade to 1.9.0
description: "Memoria 1.9.0 renames the Entity Framework Core events table to DomainEvents. How to rename an existing database before an upgraded application runs against it."
parent: Upgrading
nav_order: 3
---

# Upgrade to 1.9.0

Memoria 1.9.0 renames the Entity Framework Core event table from `events` to `DomainEvents`. It is a
schema change and nothing else: no column, index, entity, API or serialised payload changes with it,
and no row is rewritten.

It affects you only if you use `Memoria.EventSourcing.Store.EntityFrameworkCore` against a database
created by an earlier version. The Cosmos DB store has no tables and is untouched, and so is the
dynamic consistency boundary store, whose four tables were named `Dcb*` from the start.

**An existing database must be renamed before an upgraded application runs against it.** The store
reads and writes `DomainEvents` from 1.9.0 onwards; it will not find `events`, and the failure is a
missing-object error from the engine on the first read.

<a name="why-the-table-was-renamed"></a>
## Why the table was renamed

`events` was the one store table that did not say whose it was. The other two have always been
`DomainAggregates` and `DomainProjections`, and the DCB store's are `DcbEvents`, `DcbEventTags`,
`DcbTagHeads` and `DcbSnapshots`.

That matters because a `DbContext` deriving from `DomainDbContext` is expected to carry tables of
your own alongside the store's — that is the documented way to use it — and the store's database is
often shared with schema somebody else manages. `events` is a name an application, an outbox, an
audit trail or an analytics pipeline is quite likely to want, and until now Memoria took it. The
other five tables Memoria ships say who they belong to; this one now does too.

The rename costs a one-off migration and removes a collision that costs nothing to avoid.

<a name="rename-the-table"></a>
## Rename the table

### If you use EF Core migrations

Your `DbContext` derives from `DomainDbContext`, so the model already carries the new name. Generate
the migration and apply it:

```bash
dotnet ef migrations add MemoriaDomainEvents
dotnet ef database update
```

Check the generated migration before applying it. EF should produce a `RenameTable`:

```csharp
migrationBuilder.RenameTable(name: "events", newName: "DomainEvents");
```

If it produced a `DropTable` plus a `CreateTable` instead, **do not apply it**. That drops every
event you have. Replace the body with the `RenameTable` above, or use the scripts below and mark the
migration as applied.

### If you do not use EF Core migrations

Run the rename script for your engine:

- [`scripts/migrations/1.9.0-rename-events-sqlserver.sql`](../../scripts/migrations/1.9.0-rename-events-sqlserver.sql)
- [`scripts/migrations/1.9.0-rename-events-postgresql.sql`](../../scripts/migrations/1.9.0-rename-events-postgresql.sql)

Both are metadata-only: no rows are copied and no index is rebuilt, so a stream of ten million events
costs the same as a stream of ten. Both take a table-level lock for the duration, so run them while
nothing is writing.

Both are safe to run more than once — a database already holding `DomainEvents` and no `events` is
left alone.

> **Do not run the 1.9.0 install script first.** It would create an empty `DomainEvents` beside your
> populated `events`, and the store would then read the empty one. The rename script refuses to run
> when both tables exist rather than quietly doing nothing, but the install script has no way to know
> and will not stop you.

### Installing fresh

A new database needs no migration — run the install script for your engine, which creates the table
under its new name:

- [`scripts/install/1.9.0-install-sqlserver.sql`](../../scripts/install/1.9.0-install-sqlserver.sql)
- [`scripts/install/1.9.0-install-postgresql.sql`](../../scripts/install/1.9.0-install-postgresql.sql)

See [Install the store schema](install-the-store-schema.md).

<a name="what-the-rename-does-not-change"></a>
## What the rename does not change

The three indexes keep their names — `IX_Events_EventType`, `IX_Events_StreamId_CreatedDate` and
`IX_Events_StreamId_Sequence` — because they are named for the entity rather than the table, and they
follow it across the rename with no action from you.

The primary key does not: EF names it after the table, so `PK_events` becomes `PK_DomainEvents`. The
scripts rename it, and so does an EF `RenameTable`. Leaving it behind would make a migrated database
differ from one this version creates — harmless at runtime, but it makes the next migration EF
generates from your model larger than it should be.

Nothing in application code changes. `EventEntity`, `DbSet<EventEntity> Events`, every
`IDomainService` and `IDomainDbContext` member, and every stored payload are exactly as they were.

<a name="if-you-mapped-the-table-yourself"></a>
## If you mapped the table yourself

A `DbContext` that overrides the mapping keeps whatever name it sets, and needs nothing from this
guide:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    modelBuilder.Entity<EventEntity>().ToTable("events");
}
```

That remains supported. It is also the smallest possible upgrade if renaming the table is not
something you can schedule right now: pin the old name explicitly and move at your own pace.

<a name="verifying"></a>
## Verifying

After the rename, `DomainEvents` should hold `IX_Events_EventType`, `IX_Events_StreamId_CreatedDate`,
a **unique** `IX_Events_StreamId_Sequence` and a `PK_DomainEvents` primary key, with the same row
count `events` had — and no `events` table should remain.

The shipped scripts are checked on every container-test run by standing up a real 1.7.0 database,
applying the rename, and comparing every column and index against a database built from the model.
The same run asserts that an event written before the rename is read back by the store after it.

## Related

- [Install the store schema](install-the-store-schema.md)
- [Entity Framework Core configuration](../reference/configuration/ef-core.md)
- [Release notes](../release-notes.md)
