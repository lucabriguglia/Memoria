namespace Memoria.EventSourcing.Store.Cosmos.Tests.Features.DomainService;

public class GetInMemoryAggregateTests() : Store.Tests.Features.GetInMemoryAggregateTests(new DomainServiceFactory());
