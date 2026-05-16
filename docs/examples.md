# Examples

Runnable sample projects live under [`examples/`](https://github.com/lucabriguglia/Memoria/tree/main/examples) in the repository. Each demonstrates one feature area against a real (or in-memory) backing service.

| Project                                                                                                                            | What it shows                                                              | Related docs                                                       |
|------------------------------------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------------|--------------------------------------------------------------------|
| [Memoria.Examples.EventSourcing.EntityFrameworkCore](https://github.com/lucabriguglia/Memoria/tree/main/examples/Memoria.Examples.EventSourcing.EntityFrameworkCore) | Aggregate + EF Core store + DbContext extensions                           | [Quickstart: Event Sourcing](getting-started/quickstart-event-sourcing.md) · [Configuration: EF Core](reference/configuration/ef-core.md) · [EF Core Extensions](reference/ef-core-extensions.md) |
| [Memoria.Examples.EventSourcing.Cosmos](https://github.com/lucabriguglia/Memoria/tree/main/examples/Memoria.Examples.EventSourcing.Cosmos)                         | Aggregate + Cosmos DB store                                                | [Configuration: Cosmos](reference/configuration/cosmos.md)         |
| [Memoria.Examples.Messaging.ServiceBus](https://github.com/lucabriguglia/Memoria/tree/main/examples/Memoria.Examples.Messaging.ServiceBus)                         | `SendAndPublish` with Azure Service Bus                                    | [Publish to Service Bus](guides/publish-to-service-bus.md)         |
| [Memoria.Examples.Messaging.RabbitMq](https://github.com/lucabriguglia/Memoria/tree/main/examples/Memoria.Examples.Messaging.RabbitMq)                             | `SendAndPublish` with RabbitMQ                                             | [Publish to RabbitMQ](guides/publish-to-rabbitmq.md)               |
| [Memoria.Examples.Caching.Memory](https://github.com/lucabriguglia/Memoria/tree/main/examples/Memoria.Examples.Caching.Memory)                                     | `CacheableQuery` with the in-process provider                              | [Cache queries](guides/cache-queries.md)                           |
| [Memoria.Examples.Caching.Redis](https://github.com/lucabriguglia/Memoria/tree/main/examples/Memoria.Examples.Caching.Redis)                                       | `CacheableQuery` with Redis                                                | [Cache queries](guides/cache-queries.md)                           |

## Also worth a look

[EventShop](https://github.com/lucabriguglia/EventShop) — a full ecommerce demo built on Memoria, larger than anything in this repo's `examples/`.
