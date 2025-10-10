# Release Notes

## Memoria 1.0.0
_**Released 10/10/2025**_
- Rename OpenCQRS to Memoria

## OpenCQRS 7.3.0
_**Released 09/10/2025**_
- New Cosmos InMemory store provider

## OpenCQRS 7.2.0
_**Released 27/09/2025**_
- Read mode when getting an aggregate _(BREAKING CHANGE)_:
  - SnapshotOnly
  - SnapshotWithNewEvents
  - SnapshotOrCreate
  - SnapshotWithNewEventsOrCreate

## OpenCQRS 7.1.5
_**Released 15/09/2025**_
- Get aggregate with apply new events false returns now null if the aggregate doesn't exist
- Update aggregate returns null if aggregate doesn't exist

## OpenCQRS 7.1.4
_**Released 13/09/2025**_
- Upgrade Nuget packages to latest versions

## OpenCQRS 7.1.3
_**Released 13/09/2025**_
- UpdateAggregate stores a new aggregate if it doesn't exist
- Minor improvements

## OpenCQRS 7.1.2
_**Released 10/09/2025**_
- Rename IDomainEvent to IEvent

## OpenCQRS 7.1.1
_**Released 10/09/2025**_
- Rename Aggregate to AggregateRoot

## OpenCQRS 7.1.0
_**Released 10/09/2025**_
- New methods in the domain service (EntityFrameworkCore and CosmosDB):
  - Get domain events between two sequences
  - Get domain events up to a specific date
  - Get domain events from a specific date
  - Get domain events between two dates
  - Get in memory aggregate up to a specific date
  - Custom command handlers or services
- Updated XML documentation

## OpenCQRS 7.0.0
_**Released 07/09/2025**_
- Upgrade to .NET 9
- New mediator pattern with commands, queries, and notifications
- Cosmos DB store provider
- Entity Framework Core store provider
- Extensions for db context in the Entity Framework Core store provider
- Support for IdentityDbContext from ASP.NET Core Identity
- Command validation
- Command sequences
- Automatic publishing of notifications and messages (ServiceBus or RabbitMQ) on the back of a successfully processed command
- Automatic caching of query results (MemoryCache or RedisCache)
- More flexible and extensible architecture

## OpenCQRS 7.0.0-rc.1
_**Released 06/09/2025**_
- Memory Caching Provider
- Redis Caching Provider

## OpenCQRS 7.0.0-beta.6
_**Released 05/09/2025**_
- Service Bus Provider
- RabbitMQ Provider
- Automatic publishing of messages on the back of a successfully processed command

## OpenCQRS 7.0.0-beta.5
_**Released 01/09/2025**_
- Cosmos DB store provider

## OpenCQRS 7.0.0-beta.4
_**Released 29/08/2025**_
- Send and publish methods that automatically publish notifications on the back of a successfully processed command
- Automatically validate commands before they are sent to the command handler
- Command sequences that allow to chain multiple commands in a specific order

## OpenCQRS 7.0.0-beta.3
_**Released 26/08/2025**_
- Rename track methods in the Entity Framework Core store provider
- Rename database tables in the Entity Framework Core store provider

## OpenCQRS 7.0.0-beta.2
_**Released 26/08/2025**_
- Replace events with notifications

## OpenCQRS 7.0.0-beta.1 
_**Released 25/08/2025**_
- Complete rewrite of the framework
- Upgrade to .NET 9
