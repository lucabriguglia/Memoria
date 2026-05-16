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

You can use the `CosmosSetup` helper to create the database and the container if they do not exist:

```C#
cosmosSetup.CreateDatabaseAndContainerIfNotExist(throughput: 400);
```

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

- [Domain Service](../domain-service.md)
