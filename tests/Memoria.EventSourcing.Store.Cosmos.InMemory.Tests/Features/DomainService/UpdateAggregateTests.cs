namespace Memoria.EventSourcing.Store.Cosmos.InMemory.Tests.Features.DomainService;

public class UpdateAggregateTests() : Store.Tests.Features.UpdateAggregateTests(new DomainServiceFactory());
