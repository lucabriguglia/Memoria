# Entity Framework Core: PostgreSQL

The **Memoria.EventSourcing.Store.EntityFrameworkCore.Npgsql** package adds a Postgres-aware event property filter for the Entity Framework Core store provider. It exists to make `eventPropertyFilter` work correctly when the `events.Data` column is stored as `jsonb` (or `json`).

## Why this package exists

The default property filter performs a compact substring match on the JSON column. That works on plain `text` columns (SQL Server `nvarchar`, SQLite `TEXT`, Postgres `text`) but fails on Postgres `jsonb`, because `jsonb` does not preserve the original text. When read back, `jsonb` renders with a space after every colon:

| Stored as | Serialized form returned by Postgres   |
|-----------|----------------------------------------|
| `text`    | `{"Name":"Alice"}` (as written)        |
| `jsonb`   | `{"Name": "Alice"}` (canonical)        |

A `LIKE '%"Name":"Alice"%'` against `jsonb` never matches because the canonical form has the extra space. This package replaces the substring filter with one that uses the `@>` JSON-containment operator, which works regardless of formatting and is indexable with a GIN index.

## Installation

Install **Memoria.EventSourcing.Store.EntityFrameworkCore.Npgsql** alongside the core EF Core package:

```
dotnet add package Memoria.EventSourcing.Store.EntityFrameworkCore
dotnet add package Memoria.EventSourcing.Store.EntityFrameworkCore.Npgsql
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
```

## Registration

Call `AddMemoriaEntityFrameworkCoreNpgsql()` **after** `AddMemoriaEntityFrameworkCore<TDbContext>()`. It replaces the default substring filter with the Npgsql JSON filter:

```csharp
services
    .AddScoped(sp => new DbContextOptionsBuilder<DomainDbContext>()
        .UseNpgsql(connectionString)
        .UseApplicationServiceProvider(sp)
        .Options);

services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));

services.AddMemoriaEntityFrameworkCore<ApplicationDbContext>();
services.AddMemoriaEntityFrameworkCoreNpgsql();
```

That's all the wiring needed. Every `eventPropertyFilter` request issued through `IDomainService` (or the `IDomainDbContext` extension methods) now translates to a Postgres JSON containment query.

## Mapping the data column as `jsonb`

The new filter requires the `events.Data` column to be a JSON column. Configure this in your `DbContext`'s `OnModelCreating`:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    modelBuilder.Entity<EventEntity>()
        .Property(e => e.Data)
        .HasColumnType("jsonb");
}
```

Optionally add a GIN index to make property filters fast on large streams:

```csharp
modelBuilder.Entity<EventEntity>()
    .HasIndex(e => e.Data)
    .HasMethod("gin");
```

## How it translates

For an `eventPropertyFilter` of `{ "OrderId": "abc-123" }` the filter generates SQL roughly equivalent to:

```sql
WHERE "Data" @> '{"OrderId":"abc-123"}'::jsonb
```

Special characters in the property name or value are JSON-encoded before being passed to Postgres, so quotes and backslashes are safe.

## Using a custom filter

`IEventDataFilter` is a public abstraction in `Memoria.EventSourcing.Store.EntityFrameworkCore.Filtering`. Provide your own implementation and register it with `services.AddSingleton<IEventDataFilter, MyFilter>()` before calling `AddMemoriaEntityFrameworkCore<TDbContext>()`. The default registration uses `TryAdd`, so a pre-registered filter is honoured.

## Related

- [Configuration](Configuration.md)
- [Entity Framework Core](Entity-Framework-Core.md)
- [Extensions](Entity-Framework-Core-Extensions.md)
