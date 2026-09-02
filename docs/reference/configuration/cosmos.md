---
redirect_from:
  - /Cosmos.html
  - /Cosmos/
---

# Cosmos DB

Memoria provides a store provider for Cosmos DB using the SQL API.

You can use the `IDomainService` interface to access the event-sourcing functionalities. You can also use the `ICosmosDataStore` interface to access Cosmos DB specific features.

## Registration

Install the **Memoria.EventSourcing.Store.Cosmos** package, then register the provider:

```C#
services.AddMemoriaCosmos(options =>
{
    // Required
    options.Endpoint = "your-cosmosdb-endpoint";

    // Required
    options.AuthKey = "your-cosmosdb-auth-key";

    // Optional, default is "Memoria"
    options.DatabaseName = "your-database-name";

    // Optional, default is "Domain"
    options.ContainerName = "your-container-name";

    // Optional, default is new CosmosClientOptions()
    // with ApplicationName set to "Memoria"
    // and ConnectionMode set to ConnectionMode.Direct
    options.ClientOptions = new CosmosClientOptions();
});
```

`AddMemoriaCosmos` creates **one** `CosmosClient` for the application, held by a singleton
`CosmosClientProvider`. That is deliberate: a client performs its own account discovery and opens
its own connections, so one per request would pay that cost on every request. Two consequences:

- Do not dispose the client yourself. `CosmosDataStore.Dispose()` and `CosmosDomainService.Dispose()`
  are no-ops for this reason; the container disposes the provider at shutdown.
- The client is built from `CosmosOptions` once. Changing those options afterwards does not change
  the connection it holds.

Resolve `CosmosClientProvider` if you need the shared `CosmosClient` or `Container` directly.

You can use the `CosmosSetup` helper to create the database and the container if they do not exist:

```C#
cosmosSetup.CreateDatabaseAndContainerIfNotExist(throughput: 400);
```

The container is created with the [indexing policy the store is built
for](../../guides/tune-the-cosmos-container.md) — only the paths it filters or sorts on. To keep the
Cosmos DB default of indexing every path instead, pass one explicitly:

```C#
await cosmosSetup.CreateDatabaseAndContainerIfNotExist(new IndexingPolicy());
```

The policy applies only to a container this call creates. A container that already exists keeps the
policy it has, because changing it starts a background reindex during which queries can return
incomplete results. When you want that, ask for it:

```C#
await cosmosSetup.ReplaceIndexingPolicy(CosmosIndexingPolicy.CreateRecommended());
```

Applying the same policy twice is a no-op, so both are safe from a deployment step. On a container
that already holds data, do the replace during a quiet period.


## Identifiers must not collide

Events, aggregates and projections all live in one container, partitioned by stream id, and their
document ids are built from different things:

| Document   | Id                             |
|------------|--------------------------------|
| Event      | `{streamId}:{sequence}`        |
| Aggregate  | `{aggregateId}:{typeVersion}`  |
| Projection | `{projectionId}:{typeVersion}` |

So an aggregate whose id renders the same string as its stream id puts its version 1 snapshot on the
id of the event at sequence 1:

```C#
// Collides. The snapshot's document id is "order-42:1", and so is the first event's.
public class OrderStreamId(string orderId) : IStreamId
{
    public string Id => $"order-42";
}

public class OrderAggregateId(string orderId) : IAggregateId<Order>
{
    public string Id => $"order-42";
}
```

Give the aggregate or the projection an identifier that differs from the stream's — a prefix is
enough, and it is what the framework's own examples do:

```C#
public string Id => $"order:{orderId}";        // stream
public string Id => $"order-aggregate:{orderId}";  // aggregate
```

The store detects a collision rather than writing through it. A read of the wrong kind of document
returns `memoria/document-id-collision` naming both types, and a save that would have overwritten the
colliding document is refused, so the event survives. This is not retryable: it is a modelling
mistake, and it fails identically until the identifier changes.

> The InMemory Cosmos provider keeps each kind of document in its own dictionary, so it cannot
> reproduce a collision. Test identifier schemes against the emulator.

## Write limits

Cosmos DB commits at most 100 operations in one transactional batch. That turns into limits on how
many events a single call can append:

| Call | Maximum | Why |
|---|---|---|
| `SaveEvents` | 100 events | One event document each |
| `SaveAggregate` | 99 uncommitted events | One event document each, plus the aggregate document |

> **Changed in 1.7.0.** `SaveAggregate` allowed 49 events before. Each event also wrote an
> aggregate-event link document, so every event cost two batch operations. Those links are gone and
> the limit doubled.

Exceeding either is refused before anything is sent, with a
[`memoria/batch-limit-exceeded`](../../concepts/result-pattern.md#failure-classification) failure
naming both the count supplied and the maximum. These batches cannot be split: they commit
atomically with the sequence check that guards them, and splitting would let another writer
interleave. Nothing is written when the limit is hit.

Reading is not limited this way. Building or refreshing an aggregate snapshot over a stream of any
length works, because those writes go over events that are already durable and are split across as
many batches as needed.

> A batch is also capped at 2 MB. Very large payloads can still exceed it under these event counts,
> and that surfaces as an ordinary `memoria/storage-failure`.

## Diagnostics

Memoria emits diagnostic events using `System.Diagnostics` to help you monitor and troubleshoot your application.

| Event                          | Tags                                                                                                                                                                  |
|--------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **Cosmos Transactional Batch** | - operation<br/> - streamId<br/>- aggregateId<br/>- cosmos.activityId<br/>- cosmos.statusCode<br/>- cosmos.errorMessage<br/>- cosmos.requestCharge<br/>- cosmos.count |
| **Cosmos Read Item**           | - operation<br/> - streamId<br/>- aggregateId<br/>- cosmos.activityId<br/>- cosmos.statusCode<br/>- cosmos.requestCharge<br/>                                         |
| **Cosmos Feed Iterator**       | - operation<br/> - streamId<br/>- aggregateId<br/>- cosmos.activityId<br/>- cosmos.statusCode<br/>- cosmos.requestCharge<br/>- cosmos.count                           |
| **Concurrency Exception**      | - streamId<br/>- expectedEventSequence<br/>- latestEventSequence                                                                                                      |
| **Exception**                  | - operation<br/>- streamId                                                                                                                                            |

## Related

- [Tune the Cosmos DB container](../../guides/tune-the-cosmos-container.md)
- [Domain Service](../domain-service.md)
