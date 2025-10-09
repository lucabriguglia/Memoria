namespace OpenCqrs.EventSourcing.Store.Cosmos.Tests.Features.DomainService;

public class SaveAggregateTests() : OpenCqrs.EventSourcing.Store.Tests.Features.SaveAggregateTests(new DomainServiceFactory());
