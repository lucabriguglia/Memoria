namespace Memoria.EventSourcing.Store.Cosmos.InMemory.Tests.Features.DomainService;

public class GetInMemoryAggregateTests() : Store.Tests.Features.GetInMemoryAggregateTests(new DomainServiceFactory());
