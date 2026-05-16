# Configuration: Event Sourcing

To use Memoria's event sourcing features, install the **Memoria.EventSourcing** package and register it:

```C#
services.AddMemoriaEventSourcing();
```

Then pick a store provider:

- [Entity Framework Core](ef-core.md) — relational stores (SQL Server, SQLite, PostgreSQL, MySQL, In-Memory)
  - [+ ASP.NET Core Identity](ef-core-identity.md)
- [Cosmos DB](cosmos.md) — Azure Cosmos DB with the SQL API

## Related

- [Memoria Core](memoria.md)
- [Domain Service](../domain-service.md)
