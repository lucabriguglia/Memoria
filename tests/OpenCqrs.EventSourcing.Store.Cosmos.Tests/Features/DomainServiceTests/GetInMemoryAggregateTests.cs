namespace OpenCqrs.EventSourcing.Store.Cosmos.Tests.Features.DomainServiceTests;

public class GetInMemoryAggregateTests() : OpenCqrs.EventSourcing.Store.Tests.Features.GetInMemoryAggregateTests(new DomainServiceFactory());
