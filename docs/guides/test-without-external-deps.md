# Test without external dependencies

For tests and local development, Memoria ships in-process variants of the providers that normally require a real backing service. Your application code is unchanged — only the registration differs.

| Concern         | InMemory package                                                            | What it replaces                       |
|-----------------|-----------------------------------------------------------------------------|----------------------------------------|
| Event sourcing  | `Memoria.EventSourcing.Store.Cosmos.InMemory`                               | Azure Cosmos DB                        |
| Event sourcing  | EF Core's built-in `UseInMemoryDatabase` provider                           | SQL Server / SQLite / Postgres / MySQL |
| Messaging       | `Memoria.Messaging.ServiceBus.InMemory`                                     | Azure Service Bus                      |
| Messaging       | `Memoria.Messaging.RabbitMq.InMemory`                                       | RabbitMQ                               |
| Caching         | `Memoria.Caching.Memory`                                                    | (already in-process; works everywhere) |

The same `IDomainService` / `IMessagingProvider` / `ICachingProvider` contract is honoured. Tests written against the InMemory variant exercise the same code paths as production.

## Event sourcing with EF Core's in-memory provider

```C#
services
    .AddScoped(sp => new DbContextOptionsBuilder<DomainDbContext>()
        .UseInMemoryDatabase("test-db")
        .UseApplicationServiceProvider(sp)
        .Options);

services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase("test-db"));

services.AddMemoria(typeof(Program));
services.AddMemoriaEventSourcing(typeof(Program));
services.AddMemoriaEntityFrameworkCore<AppDbContext>();
```

## Event sourcing with Cosmos InMemory

```C#
services.AddMemoriaCosmosInMemory();
```

No Cosmos endpoint or key required. Data lives in-process for the lifetime of the DI container.

## Messaging InMemory variants

```C#
services.AddMemoriaServiceBusInMemory();
// or
services.AddMemoriaRabbitMqInMemory();
```

Messages are dispatched in-process to any handler registered for them, so you can assert against handler behaviour without standing up a broker.

## Caveats

The InMemory storage variants do not enforce all the durability guarantees their production counterparts do (e.g. Cosmos's RU limits, network partitions). Use them for fast feedback in tests and as a starting point in local dev — not for performance characterisation.

## Related

- [Providers](../concepts/providers.md)
- [Configuration: Event Sourcing](../reference/configuration/event-sourcing.md)
