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

Memoria also provides support for IdentityDbContext from ASP.NET Core Identity, allowing you to integrate event sourcing with user management and authentication features.

## Diagnostics

Memoria emits diagnostic events using `System.Diagnostics` to help you monitor and troubleshoot your application.

| Event                          | Tags                                                                                                                                                                  |
|--------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **Concurrency Exception**      | - streamId<br/>- expectedEventSequence<br/>- latestEventSequence                                                                                                      |
| **Exception**                  | - operation<br/>- streamId                                                                                                                                            |

## Related

- [Domain Service](Domain-Service.md)
- [Scenarios](Entity-Framework-Core-Scenarios)
- [Extensions](Entity-Framework-Core-Extensions)
