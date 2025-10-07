namespace OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Tests.Features.DomainServiceTests;

public class GetEventsTests() : OpenCqrs.EventSourcing.Store.Tests.Features.GetAggregateTests(new DomainServiceFactory());
