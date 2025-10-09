namespace OpenCqrs.EventSourcing.Store.Cosmos.Tests.Features.DomainService;

public class UpdateAggregateTests() : OpenCqrs.EventSourcing.Store.Tests.Features.UpdateAggregateTests(new DomainServiceFactory());
