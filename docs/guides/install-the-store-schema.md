# Install the store schema

The Entity Framework Core store needs three tables: `events`, `DomainAggregates`, and
`DomainProjections`. This guide covers getting them into a database, whichever way you manage schema.

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

- [`scripts/install/1.7.0-install-sqlserver.sql`](../../scripts/install/1.7.0-install-sqlserver.sql)
- [`scripts/install/1.7.0-install-postgresql.sql`](../../scripts/install/1.7.0-install-postgresql.sql)

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

## How the scripts stay honest

An install script that quietly drifts from the model would be worse than none: it would stand up a
database the store then fails against for reasons nobody can see.

So on every CI run, for each engine, the container suite builds one database from the script and
another from the model, then compares every column with its engine type and every index including
primary keys, across all three tables. Any divergence fails the build.

## Related

- [Entity Framework Core configuration](../reference/configuration/ef-core.md)
- [Upgrade to 1.7.0](upgrade-1.7.0.md)
- [Upgrade to 1.5.0](upgrade-1.5.0.md)
