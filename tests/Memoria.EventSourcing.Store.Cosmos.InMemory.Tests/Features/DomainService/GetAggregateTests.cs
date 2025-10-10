namespace Memoria.EventSourcing.Store.Cosmos.InMemory.Tests.Features.DomainService;

public class GetAggregateTests() : Store.Tests.Features.GetAggregateTests(new DomainServiceFactory());
