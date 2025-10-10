namespace Memoria.EventSourcing.Store.Cosmos.InMemory.Tests.Features.DomainService;

public class SaveAggregateTests() : Store.Tests.Features.SaveAggregateTests(new DomainServiceFactory());
