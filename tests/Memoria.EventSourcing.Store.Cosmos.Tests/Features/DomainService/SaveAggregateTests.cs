namespace Memoria.EventSourcing.Store.Cosmos.Tests.Features.DomainService;

public class SaveAggregateTests() : Store.Tests.Features.SaveAggregateTests(new DomainServiceFactory());
