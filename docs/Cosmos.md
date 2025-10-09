# Cosmos DB

Memoria provides a store provider for Cosmos DB using the SQL API.

You can use the `IDomainService` interface to access the event-sourcing functionalities.

You can also use the `ICosmosDataStore` interface to access Cosmos DB specific features.

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

[Domain Service](Domain-Service.md)
