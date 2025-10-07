namespace OpenCqrs.EventSourcing.Store.Cosmos.Tests.Features.DomainServiceTests;

public class UpdateAggregateTests() : OpenCqrs.EventSourcing.Store.Tests.Features.UpdateAggregateTests(new DomainServiceFactory());
