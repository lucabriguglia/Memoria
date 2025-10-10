namespace Memoria.EventSourcing.Store.Cosmos.Tests.Features.DomainService;

public class GetAggregateTests() : Store.Tests.Features.GetAggregateTests(new DomainServiceFactory());
