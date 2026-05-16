---
redirect_from:
  - /Entity-Framework-Core-Npgsql.html
  - /Entity-Framework-Core-Npgsql/
---

# Use PostgreSQL with `jsonb` event data

If you store the EF Core event-data column as `jsonb` (recommended on Postgres), the default event-property filter — a substring match on the text representation — silently stops matching. Postgres normalises `jsonb` to a canonical form with a space after every colon, so `LIKE '%"Name":"Alice"%'` never finds the row that contains `{"Name": "Alice"}`.

The **Memoria.EventSourcing.Store.EntityFrameworkCore.Npgsql** package replaces the default filter with one that uses the Postgres `@>` JSON-containment operator. It works regardless of whitespace formatting and is GIN-indexable.

For the registration call, see [Configuration: Entity Framework Core](../reference/configuration/ef-core.md#postgresql).

## Why this matters

| Stored as | Serialised form returned by Postgres |
|-----------|---------------------------------------|
| `text`    | `{"Name":"Alice"}` (as written)       |
| `jsonb`   | `{"Name": "Alice"}` (canonical)       |

Without the Npgsql filter, `eventPropertyFilter = { "Name": "Alice" }` queries silently miss every row stored as `jsonb`. There is no error — just an empty result set. This is the failure mode this package exists to fix.

## Map the column as `jsonb`

The new filter requires the `events.Data` column to be a JSON column. Configure this in your `DbContext`'s `OnModelCreating`:

```C#
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    modelBuilder.Entity<EventEntity>()
        .Property(e => e.Data)
        .HasColumnType("jsonb");
}
```

## Index for fast property filters

Add a GIN index when streams grow large and property filtering becomes a hot path:

```C#
modelBuilder.Entity<EventEntity>()
    .HasIndex(e => e.Data)
    .HasMethod("gin");
```

## How filters translate

For `eventPropertyFilter = { "OrderId": "abc-123" }`, the filter generates SQL roughly equivalent to:

```sql
WHERE "Data" @> '{"OrderId":"abc-123"}'::jsonb
```

Property names and values are JSON-encoded before being passed to Postgres, so quotes and backslashes are safe.

## Plugging in a custom filter

`IEventDataFilter` is a public abstraction in `Memoria.EventSourcing.Store.EntityFrameworkCore.Filtering`. Provide your own implementation and register it before calling `AddMemoriaEntityFrameworkCore<TDbContext>()`:

```C#
services.AddSingleton<IEventDataFilter, MyFilter>();
services.AddMemoriaEntityFrameworkCore<ApplicationDbContext>();
```

The default registration uses `TryAdd`, so a pre-registered filter is honoured.

## Related

- [Configuration: Entity Framework Core](../reference/configuration/ef-core.md)
- [EF Core Extensions](../reference/ef-core-extensions.md)
