namespace OpenCqrs.EventSourcing.Store.Cosmos.Tests.Features.DomainServiceTests;

public class GetAggregateTests() : OpenCqrs.EventSourcing.Store.Tests.Features.GetAggregateTests(new DomainServiceFactory());
