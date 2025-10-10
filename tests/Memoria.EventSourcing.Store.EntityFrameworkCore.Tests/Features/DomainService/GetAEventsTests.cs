namespace OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Tests.Features.DomainService;

public class GetEventsTests() : OpenCqrs.EventSourcing.Store.Tests.Features.GetAggregateTests(new DomainServiceFactory());
