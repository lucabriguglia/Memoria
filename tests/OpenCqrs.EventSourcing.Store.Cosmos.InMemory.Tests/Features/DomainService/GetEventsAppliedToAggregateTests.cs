namespace OpenCqrs.EventSourcing.Store.Cosmos.InMemory.Tests.Features.DomainService;

public class GetEventsAppliedToAggregateTests() : OpenCqrs.EventSourcing.Store.Tests.Features.GetEventsAppliedToAggregateTests(new DomainServiceFactory());
