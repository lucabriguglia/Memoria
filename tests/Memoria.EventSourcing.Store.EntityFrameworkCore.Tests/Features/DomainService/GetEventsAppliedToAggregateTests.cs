namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Tests.Features.DomainService;

public class GetEventsAppliedToAggregateTests() : Store.Tests.Features.GetEventsAppliedToAggregateTests(new DomainServiceFactory());
