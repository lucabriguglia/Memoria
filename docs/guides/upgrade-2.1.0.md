---
title: Upgrade to 2.1.0
description: "The DCB store appends through a routine on SQL Server and PostgreSQL: install it before the upgraded application appends."
parent: Upgrading
nav_order: 1
---

# Upgrade to 2.1.0
{: .no_toc }

## On this page
{: .no_toc .text-delta }

1. TOC
{:toc}

**Only the dynamic consistency boundary store needs anything**, and only on SQL Server and
PostgreSQL. The streamed store, the DCB store on SQLite and every other package upgrade by changing the
version. No table, column or serialised payload changes, so there is no data migration.

## 1. Install the DCB append routine

On SQL Server and PostgreSQL, `Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore` now appends through
a routine in the database — a stored procedure on SQL Server, a function on PostgreSQL — so that an
append is one round trip instead of four. The tables are unchanged; the routine is new, and **every
append fails until it is installed**, as `memoria/storage-failure` with a description that names the
missing routine and how to install it.

Install it before the upgraded application appends, the same way you install the tables:

| You manage schema with | Do this |
|------------------------|---------|
| EF Core migrations | Add a migration calling `migrationBuilder.CreateDcbAppendRoutine(TargetModel)` in `Up` and `migrationBuilder.DropDcbAppendRoutine(TargetModel)` in `Down`, and apply it |
| The install scripts | Run `scripts/install/2.1.0-install-dcb-sqlserver.sql` or `2.1.0-install-dcb-postgresql.sql`. Against a database installed from 1.8.0's script it adds only the routine |
| `EnsureCreated` | Call `await context.Database.EnsureDcbAppendRoutineAsync()` after `EnsureCreatedAsync()` |

```csharp
public partial class InstallDcbAppendRoutine : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.CreateDcbAppendRoutine(TargetModel);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropDcbAppendRoutine(TargetModel);
}
```

If your context maps the store's tables elsewhere, or stores payloads as `jsonb`, install the routine
with a migration or `EnsureDcbAppendRoutineAsync`, not the script: both generate it from your model.
Install it from every context that maps the tables differently. The SQL Server procedure needs SQL
Server 2016 SP1 or later at database compatibility level 130 or above.

See [Install the store schema](install-the-store-schema.md#the-dynamic-consistency-boundary-store) for
each route in full.

## 2. What else changes for a DCB application

Nothing you call changes shape. What you can observe:

- **Appends are faster on SQL Server and PostgreSQL**, and cheaper than a streamed append in the
  benchmarks. See [Streams or DCB?](choose-streams-or-dcb.md#what-it-costs).
- **A refused append can arrive sooner.** The routine checks the boundary before taking any lock, so a
  writer that has already lost is refused straight away, without waiting for the others.
- **Changes staged on the context commit with the append, and only with it**: they commit if the
  append is accepted and stay pending if it is refused. Before 2.1.0 an append that created a tag's
  first head saved them outside its transaction, so a refused append could still save them; that is
  fixed on every engine.
- **A transaction you opened yourself is joined** on SQL Server and PostgreSQL when nothing is staged
  on the context: a refusal leaves it usable, and committing it is yours to do. Before 2.1.0 such an
  append failed. With something staged, it still fails, because the store cannot open the transaction
  it saves staged changes in inside yours.
- **A context that implements `IDcbDbContext` without deriving from `DcbDbContext`** keeps appending
  through Entity Framework Core, as it did before, and needs no routine.
