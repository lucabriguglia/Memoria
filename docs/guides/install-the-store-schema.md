---
title: Install the store schema
description: "Get the Entity Framework Core store tables, and the DCB store's tables and append routine, into a database, whichever way you manage schema."
parent: Guides
nav_order: 13
---

# Install the store schema

The Entity Framework Core store needs three tables: `DomainEvents`, `DomainAggregates`, and
`DomainProjections`. This guide covers getting them into a database, whichever way you manage schema.
The dynamic consistency boundary store has tables of its own and, on SQL Server and PostgreSQL, an
append routine: see [The dynamic consistency boundary store](#the-dynamic-consistency-boundary-store).

> **`DomainEvents` is new in 1.9.0.** Before that the event table was called `events`. A database
> created by an earlier version needs the rename applied before an upgraded application runs against
> it — see [Upgrade to 1.9.0](upgrade-1.9.0.md).

You do not need to write a migration by hand either way.

## If you use EF Core migrations

Your `DbContext` derives from `DomainDbContext`, so the store's model is already part of your model.
EF generates the whole schema for you:

```bash
dotnet ef migrations add MemoriaStore
dotnet ef database update
```

That is the entire install. Memoria deliberately ships no migration files of its own: migrations
belong to the assembly and `DbContext` that own the connection string, and a second copy of the
schema in this package could drift from `OnModelCreating` without anyone noticing.

If your context adds entities of its own, they appear in the same migration — that is expected.

## If you do not use EF Core migrations

For databases managed with DbUp, Flyway, by a DBA, or by hand, run the install script for your
engine:

- [`scripts/install/1.9.0-install-sqlserver.sql`](../../scripts/install/1.9.0-install-sqlserver.sql)
- [`scripts/install/1.9.0-install-postgresql.sql`](../../scripts/install/1.9.0-install-postgresql.sql)

Both are safe to run more than once: every object is guarded, so a re-run adds only what is missing.
Both assume the default table names and the default schema (`dbo` on SQL Server, `public` on
PostgreSQL); adjust the identifiers if your `DbContext` maps them elsewhere.

The SQL Server script contains no `GO` separators, so it runs as a single batch under sqlcmd, SSMS,
Azure Data Studio, or a plain `SqlCommand`.

> **Install, not upgrade.** These scripts create what is missing and change nothing that already
> exists. To move an existing database from an earlier version, use the scripts under
> [`scripts/migrations`](../../scripts/migrations) and the matching upgrade guide.

## If you use EnsureCreated

`context.Database.EnsureCreatedAsync()` creates the schema directly from the model and needs nothing
from this guide. It has no upgrade path, though — it creates a database or does nothing — so it suits
tests and throwaway environments rather than anything you intend to migrate later.

## The dynamic consistency boundary store

`Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore` has four tables of its own — `DcbEvents`,
`DcbEventTags`, `DcbTagHeads` and `DcbSnapshots` — and shares nothing with the tables above. On SQL
Server and PostgreSQL it also needs **the append routine**: a stored procedure on SQL Server, a function
on PostgreSQL, through which every append runs as one round trip. SQLite needs no routine; there the
store appends through Entity Framework Core.

A routine is not part of the EF model, so `EnsureCreated` and `dotnet ef migrations add` create the
tables but never the routine. Install it the same way you install the tables:

| You manage schema with | Tables | Append routine |
|------------------------|--------|----------------|
| EF Core migrations | Generated from your `DcbDbContext`'s model | `migrationBuilder.CreateDcbAppendRoutine(TargetModel)` in a migration |
| Install scripts | The DCB script for your engine, below | The same script |
| `EnsureCreated` | `EnsureCreatedAsync()` | `await context.Database.EnsureDcbAppendRoutineAsync()` straight after |

All three install the same routine. Without it, every append on SQL Server or PostgreSQL fails as
`memoria/storage-failure`, with a description naming the missing routine and these three ways to
install it.

### With EF Core migrations

Add the routine to the migration that creates the tables, or to a migration of its own:

```csharp
public partial class InstallDcbAppendRoutine : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.CreateDcbAppendRoutine(TargetModel);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropDcbAppendRoutine(TargetModel);
}
```

The routine is generated from the migration's target model when the migration runs, so it fits however
your context maps the store's tables. Both helpers add nothing on SQLite, so the same migration runs on
any engine. `Down` drops only this mapping's routine.

### With the install scripts

Run the DCB script for your engine — *alongside* the script above if you use both consistency models,
not instead of it:

- [`scripts/install/2.1.0-install-dcb-sqlserver.sql`](../../scripts/install/2.1.0-install-dcb-sqlserver.sql)
- [`scripts/install/2.1.0-install-dcb-postgresql.sql`](../../scripts/install/2.1.0-install-dcb-postgresql.sql)

Each creates the four tables and the routine, and is safe to run more than once. The tables are the
same as 1.8.0's, so on a database installed from a 1.8.0 script, running the 2.1.0 one adds only the
routine. The SQL Server script needs SQL Server 2016 SP1 or later at database compatibility level 130 or
above, and creates the procedure through `EXEC` so that it can stay one batch with no `GO` separators.

The scripts assume the default table names and schema, and the default `text` payload columns on
PostgreSQL. **If your context maps the tables elsewhere, or stores payloads as `jsonb`, do not adapt the
routine by hand**: install it with a migration or `EnsureDcbAppendRoutineAsync`, which generate it
from your model.

### With EnsureCreated

```csharp
await dbContext.Database.EnsureCreatedAsync();
await dbContext.Database.EnsureDcbAppendRoutineAsync();
```

`EnsureDcbAppendRoutineAsync` creates or replaces the routine for this context's model, does nothing on
SQLite, and is safe to call every time the application starts.

### Which routine a context uses

The default model's routine is `memoria_dcb_append_v1`, in the default schema. A context that maps the
tables anywhere else — other names, columns or schema, or `jsonb` payloads — gets
`memoria_dcb_append_v1_` followed by a hash of its mapping, in the schema of its events table, so two
contexts mapping different tables in one database never replace each other's routine. Install it from
each context that maps them differently.

The `v1` is a version. A Memoria release that changes the routine's parameters ships it under a new
name beside the old one rather than replacing it, and says so in its upgrade guide; with migrations,
that means a new migration calling the helper again.

### Tag columns are case-sensitive

> **Do not relax the collation on the two `Tag` columns.** The scripts create them case-sensitively —
> `SQL_Latin1_General_CP1_CS_AS` on SQL Server, `"C"` on PostgreSQL — because tags compare ordinally
> in .NET. Under SQL Server's usual case-insensitive default, `seat:A1` and `seat:a1` become one row,
> and every consistency boundary naming them is quietly wider than the code says. A container test
> asserts the collation on both engines, and another proves the behaviour it protects.

## How the scripts stay honest

An install script that quietly drifts from the model would be worse than none: it would stand up a
database the store then fails against for reasons nobody can see.

So on every CI run, for each engine, the container suite builds one database from the script and
another from the model, then compares every column with its engine type and every index including
primary keys, across every table. Any divergence fails the build. The DCB scripts are held to the
same comparison, plus the collation of their two `Tag` columns, and each DCB script is checked to
install exactly the routine the store generates: the routine a script installs, the one a migration
installs and the one `EnsureDcbAppendRoutineAsync` installs are compared, and must be identical.

## Related

- [Entity Framework Core configuration](../reference/configuration/ef-core.md)
- [Entity Framework Core (DCB) configuration](../reference/configuration/dcb-ef-core.md)
- [Upgrade to 2.1.0](upgrade-2.1.0.md)
- [Upgrade to 1.9.0](upgrade-1.9.0.md)
- [Upgrade to 1.7.0](upgrade-1.7.0.md)
- [Upgrade to 1.5.0](upgrade-1.5.0.md)
