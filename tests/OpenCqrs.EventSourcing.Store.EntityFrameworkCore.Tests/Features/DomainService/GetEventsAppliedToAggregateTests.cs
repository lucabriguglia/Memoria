namespace OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Tests.Features.DomainService;

public class GetEventsAppliedToAggregateTests() : OpenCqrs.EventSourcing.Store.Tests.Features.GetEventsAppliedToAggregateTests(new DomainServiceFactory());
