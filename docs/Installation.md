# Installation

Installing the various packages:

Via Package Manager

    Install-Package Memoria
   
Or via .NET CLI

    dotnet add package Memoria
    
Or via Paket CLI

    paket add Memoria

## Packages

| Name                                                      | Description                                        |
|-----------------------------------------------------------|----------------------------------------------------|
| Memoria                                                  | Main package, all mediator features                |
| Memoria.Caching.Memory                                   | Cache queries with Memory Cache                    |
| Memoria.Caching.Redis                                    | Cache queries with Redis Cache                     |
| Memoria.EventSourcing                                    | Main package for Event Sourcing support            |
| Memoria.EventSourcing.Store.Cosmos                       | Event Sourcing with Cosmos DB                      |
| Memoria.EventSourcing.Store.Cosmos.InMemory              | Event Sourcing with InMemory CosmosDB              |
| Memoria.EventSourcing.Store.EntityFrameworkCore          | Event Sourcing with Entity Framework Core          |
| Memoria.EventSourcing.Store.EntityFrameworkCore.Identity | Event Sourcing with Entity Framework Core Identity |
| Memoria.Messaging.RabbitMq                               | Messaging with RabbitMQ                            |
| Memoria.Messaging.ServiceBus                             | Messaging with Service Bus                         |
| Memoria.Validation.FluentValidation                      | Command validation with FluentValidation           |
