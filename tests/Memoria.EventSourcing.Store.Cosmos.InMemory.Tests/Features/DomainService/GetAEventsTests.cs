namespace OpenCqrs.EventSourcing.Store.Cosmos.InMemory.Tests.Features.DomainService;

public class GetEventsTests() : OpenCqrs.EventSourcing.Store.Tests.Features.GetAggregateTests(new DomainServiceFactory());
