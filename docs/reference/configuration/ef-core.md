---
redirect_from:
  - /Entity-Framework-Core.html
  - /Entity-Framework-Core/
---

# Entity Framework Core

The Memoria Entity Framework Core store provider enables event sourcing persistence using Entity Framework Core.

You can either use the `IDomainService` interface to access the event sourcing functionalities or directly use them from your DbContext that inherits from `DomainDbContext` or `IdentityDomainDbContext`.

All features are implemented as extension methods on the `IDomainDbContext` interface, allowing seamless integration with your existing DbContext implementations.

It also means that you can use the Memoria mediator pattern, any other mediator library, or classic service classes without any dependencies on a specific mediator.

The event sourcing functionalities can be used with the following Entity Framework Core database providers:

- SQL Server
- SQLite
- PostgreSQL
- MySQL
- In-Memory

## Registration

Install the **Memoria.EventSourcing.Store.EntityFrameworkCore** package, then create or update your DbContext and register the provider:

```C#
// Your db context that inherits from DomainDbContext
public class ApplicationDbContext(
    DbContextOptions<DomainDbContext> options,
    TimeProvider timeProvider,
    IHttpContextAccessor httpContextAccessor)
    : DomainDbContext(options, timeProvider, httpContextAccessor)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }

    public DbSet<ItemEntity> Items { get; set; } = null!;
}

// Register the db context with the provider of your choice
services
    .AddScoped(sp => new DbContextOptionsBuilder<DomainDbContext>()
        .UseSqlite(connectionString)
        .UseApplicationServiceProvider(sp)
        .Options);

services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(configuration.GetConnectionString("DefaultConnection")));

// Register the event sourcing store provider
services.AddMemoriaEntityFrameworkCore<ApplicationDbContext>();
```

## PostgreSQL

For PostgreSQL, install the companion **Memoria.EventSourcing.Store.EntityFrameworkCore.Npgsql** package when storing the event data column as `jsonb` so that `eventPropertyFilter` queries translate correctly. The default substring filter does not match `jsonb` columns because Postgres normalizes the JSON text; this package replaces it with one that uses the `@>` JSON containment operator.

```C#
services.AddMemoriaEntityFrameworkCore<ApplicationDbContext>();
services.AddMemoriaEntityFrameworkCoreNpgsql();
```

See [Use PostgreSQL with jsonb](../../guides/use-postgres-jsonb.md) for the column mapping and indexing details.

## Identifier lengths

The store maps identifiers to bounded columns, so stream, aggregate and projection identifiers have
maximum lengths. The defaults are generous — a GUID-based identifier uses well under a tenth of the
allowance — but if you compose identifiers from user data, business keys or nested paths, the limits
below are the ones to design against.

| Identifier | Composed as | Column | Maximum |
|---|---|---|---|
| Stream id | `IStreamId.Id` | `events.StreamId`, `DomainAggregates.StreamId`, `DomainProjections.StreamId` | 255 characters |
| Aggregate store id | `{IAggregateId.Id}:{[AggregateType] version}` | `DomainAggregates.Id` | 255 characters |
| Projection store id | `{IProjectionId.Id}:{[ProjectionType] version}` | `DomainProjections.Id` | 255 characters |
| Event id | `{stream id}:{sequence}` | `events.Id` | 450 characters, already bounded by the stream id |

Exceeding one of these fails the write with a truncation error from the provider.

## ASP.NET Core Identity

Memoria also supports ASP.NET Core Identity. See [Entity Framework Core + Identity](ef-core-identity.md).

## Diagnostics

Memoria emits diagnostic events using `System.Diagnostics` to help you monitor and troubleshoot your application.

| Event                     | Tags                                                             |
|---------------------------|------------------------------------------------------------------|
| **Concurrency Exception** | - streamId<br/>- expectedEventSequence<br/>- latestEventSequence |
| **Exception**             | - operation<br/>- streamId                                       |

## Related

- [Domain Service](../domain-service.md)
- [Extensions](../ef-core-extensions.md)
- [Multiple aggregates per stream](../../guides/multiple-aggregates-per-stream.md)
- [Use PostgreSQL with jsonb](../../guides/use-postgres-jsonb.md)
