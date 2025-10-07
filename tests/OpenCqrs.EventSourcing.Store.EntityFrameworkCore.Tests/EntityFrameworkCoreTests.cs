namespace OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Tests;

public class GetAggregateEventsTests() : OpenCqrs.EventSourcing.Store.Tests.Features.GetAggregateTests(new DomainServiceFactory());
