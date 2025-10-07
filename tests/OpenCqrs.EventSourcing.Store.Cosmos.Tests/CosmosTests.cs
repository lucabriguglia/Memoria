namespace OpenCqrs.EventSourcing.Store.Cosmos.Tests;

public class CosmosGetAggregateEventsTests() : OpenCqrs.EventSourcing.Store.Tests.Features.GetAggregateTests(new DomainServiceFactory());
