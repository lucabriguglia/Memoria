namespace Memoria.EventSourcing.Store.Cosmos.Tests.Features.DomainService;

public class UpdateAggregateTests() : Store.Tests.Features.UpdateAggregateTests(new DomainServiceFactory());
