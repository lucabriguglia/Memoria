namespace Memoria.EventSourcing.Store.Cosmos.Tests.Features.DomainService;

public class GetEventsAppliedToAggregateTests() : Store.Tests.Features.GetEventsAppliedToAggregateTests(new DomainServiceFactory());
