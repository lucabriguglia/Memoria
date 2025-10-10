namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Tests.Features.DomainService;

public class GetEventsTests() : Store.Tests.Features.GetAggregateTests(new DomainServiceFactory());
