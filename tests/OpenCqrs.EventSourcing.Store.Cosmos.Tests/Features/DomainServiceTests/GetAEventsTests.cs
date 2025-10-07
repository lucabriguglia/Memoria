namespace OpenCqrs.EventSourcing.Store.Cosmos.Tests.Features.DomainServiceTests;

public class GetEventsTests() : OpenCqrs.EventSourcing.Store.Tests.Features.GetAggregateTests(new DomainServiceFactory());
