namespace Memoria.EventSourcing.Store.Cosmos.InMemory.Tests.Features.DomainService;

public class GetEventsAppliedToAggregateTests() : Store.Tests.Features.GetEventsAppliedToAggregateTests(new DomainServiceFactory());
