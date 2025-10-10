namespace Memoria.EventSourcing.Store.Cosmos.Tests.Features.DomainService;

public class GetEventsTests() : Store.Tests.Features.GetAggregateTests(new DomainServiceFactory());
