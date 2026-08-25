# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
dotnet restore                                          # Restore dependencies
dotnet build --configuration Release                    # Build entire solution
dotnet test --configuration Release                     # Run all tests
dotnet test tests/Memoria.Tests                         # Run a single test project
dotnet test --filter "FullyQualifiedName~CommandResponseTests"  # Run specific test class
dotnet pack --configuration Release --output ./packages # Create NuGet packages
```

The solution file is `Memoria.slnx` (modern .slnx format). Target framework is .NET 10.0 across all projects. CI runs on GitHub Actions (`.github/workflows/build.yml`): restore, build, test, and on main branch: pack and publish to NuGet.

## Project Overview

Memoria is a .NET framework for DDD, CQRS, and Event Sourcing. It can be used as a simple mediator or as a full event sourcing solution. The solution has 13 source projects, 13 test projects, and 6 example projects.

## Architecture

### Core (`src/Memoria`)
The mediator/CQRS core. `IDispatcher` is the central entry point for all operations:
- **Commands**: `ICommand` / `ICommand<TResponse>` with `ICommandHandler<,>` — supports fire-and-forget, with-response, send-and-publish (command + notifications), and command sequences
- **Queries**: `IQuery<TResult>` with `IQueryHandler<,>` — includes `CacheableQuery` for cached queries
- **Notifications**: `INotification` with `INotificationHandler<>` — fan-out to multiple handlers
- **Results**: `Result` and `Result<T>` are discriminated unions (via `OneOf` library) of `Success`/`Failure` — used as return types throughout the framework instead of exceptions

DI registration uses `services.AddMemoria(typeof(Program))` which auto-discovers handlers by scanning the assemblies of the supplied types for closed implementations of `ICommandHandler<>`, `ICommandHandler<,>`, `IQueryHandler<,>`, and `INotificationHandler<>`.

### Event Sourcing (`src/Memoria.EventSourcing`)
Built on top of core. Key concepts:
- **EventSourcedModel**: Abstract base shared by write and read models. Holds the identity common to both (`StreamId`), plus `Version`, `EventTypeFilter`, `IsEventHandled`, and the abstract `Apply<T>(event)` / `Apply(events)` for state transitions. Instance identity is not shared: `AggregateId` lives on `AggregateRoot`/`IAggregateRoot`, `ProjectionId` on `Projection`/`IProjection`
- **AggregateRoot** (write model): `EventSourcedModel` plus `AggregateId`, `Add(event)` to stage events, and `UncommittedEvents`
- **Projection** (read model): `EventSourcedModel` plus `ProjectionId` — no `Add`/`UncommittedEvents`. Built by applying events, then persisted as a snapshot. Identified by `IProjectionId<T>` + `[ProjectionType]`
- **IDomainService**: Primary service for saving/retrieving aggregates with four read modes (`SnapshotOnly`, `SnapshotWithNewEvents`, `SnapshotOrCreate`, `SnapshotWithNewEventsOrCreate`). Also `SaveProjection`/`GetProjection` — each store uses a **dedicated projection type** keyed by `{projectionId}:{version}` with no optimistic-concurrency check: EF Core persists a `ProjectionEntity` in its own `DomainProjections` table; Cosmos persists a `ProjectionDocument` (`documentType: "Projection"`) in the same container as aggregates. `GetInMemoryAggregate`/`GetInMemoryProjection` fold events without persisting a snapshot (overloads for full stream, up-to-sequence, or up-to-date)
- **IStreamId** / **IAggregateId**: Multiple aggregates can share a single stream
- **TypeBindings** / **InstanceFactory**: Map event/aggregate/projection types to CLR types for deserialization (populated by `AddMemoriaEventSourcing` assembly scanning)

### Provider Pattern
Each concern (storage, messaging, caching, validation) follows an abstraction + provider pattern. The core registers default no-op/in-memory providers; real providers replace them:

| Concern | Abstraction | Providers |
|---------|------------|-----------|
| Storage | `IDomainService` | EF Core, EF Core + Identity, Cosmos DB, Cosmos InMemory |
| Messaging | `IMessagingProvider` | Azure Service Bus, RabbitMQ (each with InMemory variant) |
| Caching | `ICachingProvider` | Memory, Redis |
| Validation | `IValidationProvider` | FluentValidation |

InMemory variants (Cosmos, ServiceBus, RabbitMQ) exist specifically for testing without external dependencies.

### EF Core Store (`src/Memoria.EventSourcing.Store.EntityFrameworkCore`)
Uses `DomainDbContext` with three entities: `EventEntity`, `AggregateEntity`, `AggregateEventEntity`. Provides extensive `DbContext` extension methods that implement all `IDomainService` operations. Includes `AuditInterceptor` for automatic audit tracking.

## Test Stack

- **xUnit** 2.9.3 — test framework
- **FluentAssertions** 7.2.0 — assertion library
- **NSubstitute** 5.3.0 — mocking library

Test projects mirror source projects (e.g., `Memoria.Tests` tests `Memoria`, `Memoria.Caching.Memory.Tests` tests `Memoria.Caching.Memory`). Tests are organized under `Features/` and `Models/` directories.

## Solution-Wide Build Properties

`Directory.Build.props` at the root sets shared metadata (version, author, license). All projects default to `IsPackable=false`; source projects override to `true`. All projects use `ImplicitUsings` and `Nullable` enabled.

## DI Registration Pattern

```csharp
services.AddMemoria(typeof(Program));                           // Core CQRS
services.AddMemoriaEventSourcing(typeof(Program));              // Event sourcing
services.AddMemoriaEntityFrameworkCore<MyDbContext>();           // EF Core store
services.AddMemoriaFluentValidation();                          // Validation
```

Each `AddMemoria*` extension scans the provided assembly types for handlers and registers them with scoped lifetime.
