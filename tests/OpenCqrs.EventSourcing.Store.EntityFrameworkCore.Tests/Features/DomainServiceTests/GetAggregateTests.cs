namespace OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Tests.Features.DomainServiceTests;

public class GetAggregateTests() : OpenCqrs.EventSourcing.Store.Tests.Features.GetAggregateTests(new DomainServiceFactory());
