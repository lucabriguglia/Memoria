namespace OpenCqrs.EventSourcing.Store.Cosmos.Tests.Features.DomainService;

public class GetAggregateTests() : OpenCqrs.EventSourcing.Store.Tests.Features.GetAggregateTests(new DomainServiceFactory());
