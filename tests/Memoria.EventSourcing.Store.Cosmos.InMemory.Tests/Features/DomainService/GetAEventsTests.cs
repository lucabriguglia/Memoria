namespace Memoria.EventSourcing.Store.Cosmos.InMemory.Tests.Features.DomainService;

public class GetEventsTests() : Store.Tests.Features.GetAggregateTests(new DomainServiceFactory());
