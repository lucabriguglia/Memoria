namespace OpenCqrs.EventSourcing.Store.Cosmos.Tests.Features.DomainServiceTests;

public class GetEventsAppliedToAggregateTests() : OpenCqrs.EventSourcing.Store.Tests.Features.GetEventsAppliedToAggregateTests(new DomainServiceFactory());
