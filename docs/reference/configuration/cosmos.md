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


## Write limits

Cosmos DB commits at most 100 operations in one transactional batch, and the store writes more than
one document per event. That turns into limits on how many events a single call can append:

| Call | Maximum | Why |
|---|---|---|
| `SaveEvents` | 100 events | One event document each |
| `SaveAggregate` | 49 uncommitted events | One event document and one aggregate-event link per event, plus the aggregate document |

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
