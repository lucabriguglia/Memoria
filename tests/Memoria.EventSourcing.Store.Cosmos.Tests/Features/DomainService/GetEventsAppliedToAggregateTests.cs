namespace OpenCqrs.EventSourcing.Store.Cosmos.Tests.Features.DomainService;

public class GetEventsAppliedToAggregateTests() : OpenCqrs.EventSourcing.Store.Tests.Features.GetEventsAppliedToAggregateTests(new DomainServiceFactory());
