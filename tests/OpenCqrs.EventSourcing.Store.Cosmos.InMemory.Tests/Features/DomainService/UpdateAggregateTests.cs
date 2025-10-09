namespace OpenCqrs.EventSourcing.Store.Cosmos.InMemory.Tests.Features.DomainService;

public class UpdateAggregateTests() : OpenCqrs.EventSourcing.Store.Tests.Features.UpdateAggregateTests(new DomainServiceFactory());
