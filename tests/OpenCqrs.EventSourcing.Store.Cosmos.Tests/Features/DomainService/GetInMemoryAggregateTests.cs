namespace OpenCqrs.EventSourcing.Store.Cosmos.Tests.Features.DomainService;

public class GetInMemoryAggregateTests() : OpenCqrs.EventSourcing.Store.Tests.Features.GetInMemoryAggregateTests(new DomainServiceFactory());
