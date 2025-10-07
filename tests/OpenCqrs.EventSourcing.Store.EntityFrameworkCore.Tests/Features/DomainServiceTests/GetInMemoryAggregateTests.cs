namespace OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Tests.Features.DomainServiceTests;

public class GetInMemoryAggregateTests() : OpenCqrs.EventSourcing.Store.Tests.Features.GetInMemoryAggregateTests(new DomainServiceFactory());
