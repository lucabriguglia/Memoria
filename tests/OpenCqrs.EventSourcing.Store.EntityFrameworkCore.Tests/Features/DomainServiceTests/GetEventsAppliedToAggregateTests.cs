namespace OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Tests.Features.DomainServiceTests;

public class GetEventsAppliedToAggregateTests() : OpenCqrs.EventSourcing.Store.Tests.Features.GetEventsAppliedToAggregateTests(new DomainServiceFactory());
